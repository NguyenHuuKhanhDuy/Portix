using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Portix.Client.Api;
using Portix.Client.Cli.Commands;
using Portix.Client.Inspector;
using Portix.Client.Tunneling;
using Spectre.Console.Cli;

// Kestrel offers h2c (HTTP/2 without TLS) to the tunnel server; the .NET HttpClient refuses
// to negotiate HTTP/2 over plaintext unless this switch is set before first use.
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// Dual-mode entry point: a recognized CLI subcommand runs this binary as the thin client, which
// talks to a running daemon (self-relaunching this same binary with no arguments if none is
// reachable — see DaemonLauncher). Anything else — no arguments, or the self-relaunch above —
// falls through and starts this binary as the daemon itself. The check happens before
// WebApplication.CreateBuilder so the CLI path never touches ASP.NET Core's configuration
// binding, which expects `--key value`-shaped argv, not positional subcommands.
var cliSubcommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "ls", "rm", "login", "logout" };
if (args.Length > 0 && cliSubcommands.Contains(args[0]))
{
    var cli = new CommandApp();
    cli.Configure(config =>
    {
        config.SetApplicationName("portix");
        config.SetApplicationVersion("0.1.0");
        config.AddCommand<HttpCommand>("http");
        config.AddCommand<LsCommand>("ls");
        config.AddCommand<RmCommand>("rm");
        config.AddCommand<LoginCommand>("login");
        config.AddCommand<LogoutCommand>("logout");
    });

    return await cli.RunAsync(args);
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

builder.Services.AddKeyedSingleton<HttpClient>("local", (_, _) => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
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

app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
return 0;
