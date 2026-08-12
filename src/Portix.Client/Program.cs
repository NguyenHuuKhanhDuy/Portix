using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Portix.Client.Api;
using Portix.Client.Cli;
using Portix.Client.Cli.Commands;
using Portix.Client.Inspector;
using Portix.Client.Tunneling;
using Spectre.Console.Cli;

// Kestrel offers h2c (HTTP/2 without TLS) to the tunnel server; the .NET HttpClient refuses
// to negotiate HTTP/2 over plaintext unless this switch is set before first use.
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// Dual-mode entry point: a human-typed invocation always runs this binary as the thin CLI
// client, which talks to a running daemon (self-relaunching this same binary if none is
// reachable — see DaemonLauncher). Only that internal relaunch — never anything a human could
// type — sets PORTIX_RUN_DAEMON, which is what actually starts this binary as the daemon itself.
// Everything else, including no arguments, -h/--help, or an invalid subcommand, goes through the
// CLI framework, so Spectre.Console.Cli's own usage/help/error handling is what a human ever sees
// for those cases — none of it reaches ASP.NET Core's configuration binding below.
if (Environment.GetEnvironmentVariable("PORTIX_RUN_DAEMON") != "1")
{
    // Double-clicking portix.exe from Explorer is indistinguishable from typing bare `portix`
    // into an existing shell by args alone (args.Length == 0 either way) — but Explorer creates
    // a brand-new console owned solely by this process, whereas typing it into cmd/PowerShell/
    // Windows Terminal runs it inside a console that shell is also attached to. Only the former
    // case gets a persistent, ready-to-use console instead of --help flashing and closing.
    if (args.Length == 0 && DoubleClickLaunchDetector.IsLikelyDoubleClicked())
    {
        // AppContext.BaseDirectory is *not* where the exe actually lives for a self-contained
        // single-file publish — it's .NET's per-run temp self-extraction directory (the bundled
        // native host/runtime get extracted there to actually execute). Environment.ProcessPath
        // is the real, running .exe's own path regardless of build mode — the same distinction
        // DaemonLauncher.GetSelfRelaunchCommand() already relies on for the same reason.
        var exeDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

        Process.Start(new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = true,
            WorkingDirectory = exeDirectory,
            Arguments = "/K \"echo Portix CLI - type: portix http ^<port^>   (or portix --help for all commands)\"",
        });
        return 0;
    }

    var cli = new CommandApp();
    cli.Configure(config =>
    {
        config.SetApplicationName("portix");
        config.SetApplicationVersion("0.1.0");
        config.AddCommand<HttpCommand>("http").WithDescription("Expose a local HTTP port through a public tunnel");
        config.AddCommand<HttpsCommand>("https").WithDescription("Expose a local HTTPS port through a public tunnel");
        config.AddCommand<LsCommand>("ls").WithDescription("List currently open tunnels");
        config.AddCommand<RmCommand>("rm").WithDescription("Close a tunnel by id");
        config.AddCommand<LoginCommand>("login").WithDescription("Save a personal API token for this machine");
        config.AddCommand<LogoutCommand>("logout").WithDescription("Remove the saved API token");
    });

    // With no default command configured, Spectre.Console.Cli's own behavior for zero
    // arguments isn't guaranteed to show help — make that case explicit rather than assume it.
    var effectiveArgs = args.Length == 0 ? new[] { "--help" } : args;
    return await cli.RunAsync(effectiveArgs);
}

var builder = WebApplication.CreateBuilder(args);

// Highest-precedence config source: a token saved via `portix login` always wins over
// appsettings.json/appsettings.{Environment}.json, regardless of which environment the daemon
// happens to run under. reloadOnChange means a login while the daemon is already running (and
// stuck retrying a bad token) takes effect on the daemon's very next reconnect attempt.
builder.Configuration.AddJsonFile(Portix.Client.Cli.ClientConfigStore.ConfigPath, optional: true, reloadOnChange: true);

var localApiPort = builder.Configuration.GetValue("Portix:LocalApiPort", 4040);

builder.WebHost.ConfigureKestrel(options =>
{
    // Local API/dashboard: loopback only. This API has no auth, so it must never be reachable off-box.
    options.Listen(IPAddress.Loopback, localApiPort);
});

builder.Services.AddSignalR();

// AllowAutoRedirect must stay false: a 3xx from the local app has to reach the public caller
// unmodified so their own browser follows it against the public tunnel domain, not have this
// HttpClient resolve it internally against localhost (which also breaks outright, since the
// redirect follow needs to resend the request body and that body is a single-read network stream).
//
// Certificate validation is skipped unconditionally (not per-request) because this specific
// keyed HttpClient has exactly one purpose in this codebase: reaching a tunnel's own
// "localhost:{port}" target (RequestForwarder, and the replay endpoint) — it never dials
// anywhere else. That's exactly the case (a developer's own self-signed/dev-mode HTTPS
// certificate) this needs to tolerate; it is not a general "don't validate certs" setting.
builder.Services.AddKeyedSingleton<HttpClient>("local", (_, _) =>
    new HttpClient(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
        },
    })
    { Timeout = Timeout.InfiniteTimeSpan });
builder.Services.AddSingleton<TunnelManager>();
builder.Services.AddSingleton<RequestStore>();
builder.Services.AddSingleton<RequestForwarder>(sp => new RequestForwarder(
    sp.GetRequiredKeyedService<HttpClient>("local"),
    new Uri(sp.GetRequiredService<IConfiguration>()["Portix:ServerUrl"] ?? "http://localhost:5100"),
    sp.GetRequiredService<TunnelManager>(),
    sp.GetRequiredService<RequestStore>(),
    sp.GetRequiredService<ILogger<RequestForwarder>>()));
builder.Services.AddHostedService<ControlChannelBackgroundService>();

var app = builder.Build();

var effectiveServerUrl = app.Configuration["Portix:ServerUrl"] ?? "http://localhost:5100";
var serverUrlSource = File.Exists(Portix.Client.Cli.ClientConfigStore.ConfigPath)
    ? $"persisted config store ({Portix.Client.Cli.ClientConfigStore.ConfigPath})"
    : "appsettings.json";
app.Logger.LogInformation("Using server URL {ServerUrl} (from {Source})", effectiveServerUrl, serverUrlSource);

// An isolated daemon (see DaemonLauncher.StartIsolatedAsync) binds an OS-assigned free port and
// tells its spawning CLI process which one it got by writing it to this file once Kestrel has
// actually bound it — the CLI can't know the port in advance since it asked for port 0.
var portAnnounceFile = Environment.GetEnvironmentVariable("PORTIX_PORT_ANNOUNCE_FILE");
if (portAnnounceFile is not null)
{
    app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() =>
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
        if (address is not null)
        {
            File.WriteAllText(portAnnounceFile, new Uri(address).Port.ToString());
        }
    });
}

// Push realtime updates to the dashboard as soon as the pieces that generate them exist.
var hubContext = app.Services.GetRequiredService<IHubContext<DashboardHub>>();
app.Services.GetRequiredService<TunnelManager>().TunnelChanged += tunnel =>
    _ = hubContext.Clients.All.SendAsync("TunnelStatusChanged", TunnelDto.From(tunnel));
app.Services.GetRequiredService<RequestStore>().Captured += capture =>
    _ = hubContext.Clients.All.SendAsync("RequestCaptured", CapturedRequestSummaryDto.From(capture));

app.MapTunnelEndpoints();
app.MapRequestEndpoints();
app.MapHub<DashboardHub>("/hubs/dashboard");

// Served from the assembly's embedded wwwroot (see Portix.Client.csproj), not a physical folder —
// works identically whether this runs as a normal multi-file build or a single-file publish with
// nothing else alongside it. IncludeAllContentForSelfExtract alone does not make ASP.NET Core's
// WebRootPath resolution find self-extracted content, so a physical-file-based UseStaticFiles()
// would silently fail to find wwwroot in the single-file-alone case.
var embeddedWebRoot = new ManifestEmbeddedFileProvider(Assembly.GetExecutingAssembly(), "wwwroot");
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = embeddedWebRoot });
app.UseStaticFiles(new StaticFileOptions { FileProvider = embeddedWebRoot });

app.Run();
return 0;
