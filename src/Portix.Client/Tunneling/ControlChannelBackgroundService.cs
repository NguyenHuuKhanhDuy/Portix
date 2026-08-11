using System.Net.Http;

namespace Portix.Client.Tunneling;

/// <summary>Owns the connect/reconnect loop for the daemon's single control channel to the server.</summary>
public sealed class ControlChannelBackgroundService : BackgroundService
{
    private static readonly TimeSpan[] BackoffSchedule =
    {
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30),
    };

    private readonly TunnelManager _tunnelManager;
    private readonly RequestForwarder _requestForwarder;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ControlChannelBackgroundService> _logger;

    public ControlChannelBackgroundService(
        TunnelManager tunnelManager,
        RequestForwarder requestForwarder,
        IConfiguration configuration,
        ILogger<ControlChannelBackgroundService> logger)
    {
        _tunnelManager = tunnelManager;
        _requestForwarder = requestForwarder;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var attempt = 0;
        var everConnected = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Re-read on every attempt (rather than once at construction) so `portix login`
            // reaches an already-running, already-stuck daemon on its very next reconnect —
            // and so a missing token here is just another failed attempt, not a startup crash.
            var serverUrl = _configuration["Portix:ServerUrl"] ?? "http://localhost:5100";
            var serverBaseUri = new Uri(serverUrl);
            var token = _configuration["Portix:Token"];

            // A fresh HttpClient (and thus a fresh SocketsHttpHandler connection pool) per attempt
            // keeps each reconnect's connection state independent of any previous, now-dead one.
            using var serverClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            ControlChannelClient? channel = null;
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    throw new InvalidOperationException(
                        "No token configured. Run `portix login <token>`, set Portix:Token in appsettings.json, or set the Portix__Token environment variable.");
                }

                channel = new ControlChannelClient(serverClient, serverBaseUri, token);
                var handshake = await channel.ConnectAsync(stoppingToken).ConfigureAwait(false);
                _tunnelManager.SetChannel(channel);

                // RegisterTunnel replies (used by ReregisterAllAsync below) are only ever observed
                // by the read loop inside RunAsync, so that loop must already be running before we
                // send anything expecting a reply — otherwise the reply arrives with no one reading
                // for it, and the caller times out waiting.
                var runTask = channel.RunAsync(
                    (streamId, tunnelId) => _requestForwarder.HandleStreamAsync(streamId, tunnelId, serverClient, stoppingToken),
                    stoppingToken);

                if (everConnected)
                {
                    await _tunnelManager.ReregisterAllAsync(stoppingToken).ConfigureAwait(false);
                }

                attempt = 0;
                everConnected = true;
                _logger.LogInformation("Connected to server (session {SessionId}).", handshake.SessionId);

                await runTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Control channel {State}.", everConnected ? "connection lost" : "failed to connect");
                _tunnelManager.SetChannel(null);
                _tunnelManager.MarkAllDisconnected(ex.Message);
            }
            finally
            {
                if (channel is not null)
                {
                    await channel.DisposeAsync().ConfigureAwait(false);
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var delay = BackoffSchedule[Math.Min(attempt, BackoffSchedule.Length - 1)];
            attempt++;

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
