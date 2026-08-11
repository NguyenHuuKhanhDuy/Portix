using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Portix.Client.Cli;

/// <summary>
/// Windows Job Object helper: ties a spawned process's lifetime to the current process, so the OS
/// kills the child the moment this process ends — for any reason, including a console window close
/// or a crash that never runs our own cleanup code.
/// </summary>
[SupportedOSPlatform("windows")]
public static class Win32JobObject
{
    // Job handles kept alive for the lifetime of this process; the OS closes them (and, per
    // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE, kills every assigned process) automatically on exit.
    private static readonly List<SafeJobObjectHandle> KeepAlive = [];
    private static readonly object KeepAliveLock = new();

    /// <summary>Creates a new job object and assigns <paramref name="process"/> to it. Non-fatal on failure.</summary>
    public static void AssignToNewJob(Process process)
    {
        var jobHandle = CreateJobObject(IntPtr.Zero, null);
        if (jobHandle.IsInvalid)
        {
            Console.WriteLine($"Warning: could not create job object to tie the daemon's lifetime to this process (error {Marshal.GetLastWin32Error()}); the daemon may outlive this console.");
            return;
        }

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
            },
        };

        var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var infoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, infoPtr, false);

            if (!SetInformationJobObject(jobHandle, JobObjectExtendedLimitInformation, infoPtr, (uint)length))
            {
                Console.WriteLine($"Warning: could not configure job object to kill the daemon on close (error {Marshal.GetLastWin32Error()}); the daemon may outlive this console.");
                jobHandle.Dispose();
                return;
            }

            if (!AssignProcessToJobObject(jobHandle, process.Handle))
            {
                Console.WriteLine($"Warning: could not tie the daemon process to this console (error {Marshal.GetLastWin32Error()}); the daemon may outlive this console.");
                jobHandle.Dispose();
                return;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
        }

        // Keep the handle alive for the process's lifetime — letting it be GC'd/disposed would close
        // it early and immediately kill the very process we just tied to it.
        lock (KeepAliveLock)
        {
            KeepAlive.Add(jobHandle);
        }
    }

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    private sealed class SafeJobObjectHandle() : SafeHandle(IntPtr.Zero, ownsHandle: true)
    {
        public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeJobObjectHandle CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(SafeJobObjectHandle hJob, int jobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(SafeJobObjectHandle hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
