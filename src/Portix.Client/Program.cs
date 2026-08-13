using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Reflection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using Portix.Client.Api;
using Portix.Client.Cli;
using Portix.Client.Cli.Commands;
using Portix.Client.Inspector;
using Portix.Client.Tunneling;
using Spectre.Console.Cli;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

if (Environment.GetEnvironmentVariable("PORTIX_RUN_DAEMON") != "1")
{
    if (args.Length == 0 && DoubleClickLaunchDetector.IsLikelyDoubleClicked())
    {
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
        config.SetApplicationVersion(VersionInfo.Current);
        config.AddCommand<HttpCommand>("http").WithDescription("Expose a local HTTP port through a public tunnel");
        config.AddCommand<HttpsCommand>("https").WithDescription("Expose a local HTTPS port through a public tunnel");
        config.AddCommand<LsCommand>("ls").WithDescription("List currently open tunnels");
        config.AddCommand<RmCommand>("rm").WithDescription("Close a tunnel by id");
        config.AddCommand<RestoreCommand>("restore").WithDescription("Reopen the tunnels from the last session (e.g. after a restart)");
        config.AddCommand<LoginCommand>("login").WithDescription("Save a personal API token for this machine");
        config.AddCommand<LogoutCommand>("logout").WithDescription("Remove the saved API token");
        config.AddCommand<UpdateCommand>("update").WithDescription("Update to the latest released version");
    });
    
    var effectiveArgs = args.Length == 0 ? new[] { "--help" } : args;
    return await cli.RunAsync(effectiveArgs);
}

const string DefaultServerUrl = "http://localhost:5100";

var builder = WebApplication.CreateBuilder(args);

var embeddedAppSettingsName = Assembly.GetExecutingAssembly().GetManifestResourceNames()
    .FirstOrDefault(name => name.EndsWith("appsettings.json", StringComparison.Ordinal));
if (embeddedAppSettingsName is not null)
{
    // Not disposed here: the source may read it lazily rather than immediately, and this is a
    // resource stream over the assembly's own in-memory image (no OS file handle), so there's
    // nothing meaningful to release early anyway.
    var embeddedAppSettingsStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedAppSettingsName);
    if (embeddedAppSettingsStream is not null)
    {
        builder.Configuration.Sources.Insert(0, new JsonStreamConfigurationSource { Stream = embeddedAppSettingsStream });
    }
}

builder.Configuration.AddJsonFile(Portix.Client.Cli.ClientConfigStore.ConfigPath, optional: true, reloadOnChange: true);

var localApiPort = builder.Configuration.GetValue("Portix:LocalApiPort", 4040);

// Only the isolated per-invocation daemon (see DaemonLauncher.StartIsolatedAsync) opts into
// this: it has a preferred-but-not-guaranteed port (so the dashboard URL is stable across runs)
// and already announces whatever port it actually binds via PORTIX_PORT_ANNOUNCE_FILE. The
// shared daemon must stay at its configured port or fail outright — other CLI invocations expect
// to find it there specifically, with no such announce-and-discover mechanism.
//
// Checked via the OS's own active-listener table (IPGlobalProperties), not by trying to bind a
// throwaway socket ourselves first: a TcpListener probe can report a port as free even when
// Kestrel's own socket transport would immediately fail to bind it (different default socket
// options) — confirmed by direct testing, not assumed.
if (localApiPort != 0
    && Environment.GetEnvironmentVariable("PORTIX_ALLOW_PORT_FALLBACK") == "1"
    && IsPortInUse(localApiPort))
{
    localApiPort = 0;
}

builder.WebHost.ConfigureKestrel(options =>
{
    // Local API/dashboard: loopback only. This API has no auth, so it must never be reachable off-box.
    options.Listen(IPAddress.Loopback, localApiPort);
});

static bool IsPortInUse(int port) =>
    IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(ep => ep.Port == port);

builder.Services.AddSignalR();

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
    new Uri(sp.GetRequiredService<IConfiguration>()["Portix:ServerUrl"] ?? DefaultServerUrl),
    sp.GetRequiredService<TunnelManager>(),
    sp.GetRequiredService<RequestStore>(),
    sp.GetRequiredService<ILogger<RequestForwarder>>()));
builder.Services.AddHostedService<ControlChannelBackgroundService>();

var app = builder.Build();

var effectiveServerUrl = app.Configuration["Portix:ServerUrl"] ?? DefaultServerUrl;
var serverUrlSource = File.Exists(Portix.Client.Cli.ClientConfigStore.ConfigPath)
    ? $"persisted config store ({Portix.Client.Cli.ClientConfigStore.ConfigPath})"
    : "built-in default — run 'portix login <token> --server <url>' to configure a real one";
app.Logger.LogInformation("Using server URL {ServerUrl} (from {Source})", effectiveServerUrl, serverUrlSource);

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
var tunnelManager = app.Services.GetRequiredService<TunnelManager>();
tunnelManager.TunnelChanged += tunnel =>
    _ = hubContext.Clients.All.SendAsync("TunnelStatusChanged", TunnelDto.From(tunnel));
app.Services.GetRequiredService<RequestStore>().Captured += capture =>
    _ = hubContext.Clients.All.SendAsync("RequestCaptured", CapturedRequestSummaryDto.From(capture));

// Remembers opened tunnels to disk (see TunnelSessionStore) so `portix restore` can bring them
// back after a crash or machine restart. Upserts only the one port that just changed — never a
// blind full-file overwrite — so two separate `portix http` invocations (each its own isolated
// daemon process) merge their entries into the same file instead of clobbering each other's.
// Deliberately does nothing on close: an entry is only ever added/updated when a tunnel opens,
// never removed just because it was closed — closing (Ctrl+C, `portix rm`, dashboard) leaves its
// entry as-is, so `portix restore` still offers it back later even after a deliberate close.
tunnelManager.TunnelChanged += tunnel =>
{
    if (tunnel.Status != TunnelStatus.Closed)
    {
        TunnelSessionStore.Upsert(new PersistedTunnel(tunnel.Scheme, tunnel.LocalPort, tunnel.Subdomain));
    }
};

app.MapTunnelEndpoints();
app.MapRequestEndpoints();
app.MapSystemEndpoints();
app.MapHub<DashboardHub>("/hubs/dashboard");

var embeddedWebRoot = new ManifestEmbeddedFileProvider(Assembly.GetExecutingAssembly(), "wwwroot");
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = embeddedWebRoot });
app.UseStaticFiles(new StaticFileOptions { FileProvider = embeddedWebRoot });

app.Run();
return 0;
