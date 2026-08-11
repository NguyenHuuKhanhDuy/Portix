namespace Portix.Server.Sessions;

/// <summary>Periodically tears down sessions that haven't sent a heartbeat within the timeout window.</summary>
public sealed class HeartbeatSweepService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(45);

    private readonly SessionRegistry _registry;
    private readonly ILogger<HeartbeatSweepService> _logger;

    public HeartbeatSweepService(SessionRegistry registry, ILogger<HeartbeatSweepService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var session in _registry.ActiveSessions)
            {
                if (now - session.LastHeartbeatUtc > HeartbeatTimeout)
                {
                    _logger.LogWarning(
                        "Session {SessionId} ({TunnelCount} tunnel(s)) missed heartbeat deadline; tearing down.",
                        session.SessionId, session.Tunnels.Count);
                    _registry.Teardown(session, new TimeoutException("Heartbeat timeout."));
                }
            }
        }
    }
}
