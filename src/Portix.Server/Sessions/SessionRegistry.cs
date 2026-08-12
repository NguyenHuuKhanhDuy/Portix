using System.Collections.Concurrent;
using System.Security.Cryptography;
using Portix.Server.Data;
using Portix.Server.Forwarding;

namespace Portix.Server.Sessions;

public sealed class SessionRegistry
{
    private const string SubdomainAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const int SubdomainLength = 8;
    private const int MaxGenerationAttempts = 20;

    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, Tunnel> _tunnelsBySubdomain = new();
    private readonly StreamBroker _streamBroker;
    private readonly ILogger<SessionRegistry> _logger;

    public SessionRegistry(StreamBroker streamBroker, ILogger<SessionRegistry> logger)
    {
        _streamBroker = streamBroker;
        _logger = logger;
    }

    /// <summary>Creates a new, tunnel-less session for a just-authenticated control channel.</summary>
    public Session CreateSession(Stream controlResponseStream, User owner)
    {
        var session = new Session
        {
            SessionId = Guid.NewGuid().ToString("N"),
            ControlResponseStream = controlResponseStream,
            Owner = owner,
        };

        _sessions[session.SessionId] = session;
        return session;
    }

    /// <summary>Sums the tunnels currently registered across every active session owned by the same user, so a user can't multiply their plan's quota by opening extra sessions.</summary>
    public int CountActiveTunnels(Guid userId) =>
        _sessions.Values.Where(s => s.Owner.Id == userId).Sum(s => s.Tunnels.Count);

    /// <summary>Registers a new tunnel under an existing session, assigning or validating its subdomain.</summary>
    public bool TryRegisterTunnel(Session session, int localPort, string? desiredSubdomain, out Tunnel? tunnel, out string? rejectionReason)
    {
        var maxConcurrentTunnels = session.Owner.Plan?.MaxConcurrentTunnels
            ?? throw new InvalidOperationException($"Session owner {session.Owner.Id} was loaded without its Plan.");

        if (CountActiveTunnels(session.Owner.Id) >= maxConcurrentTunnels)
        {
            tunnel = null;
            rejectionReason = $"Plan limit reached ({maxConcurrentTunnels}/{maxConcurrentTunnels} tunnels). Upgrade your plan for more concurrent tunnels.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(desiredSubdomain))
        {
            tunnel = new Tunnel
            {
                TunnelId = Guid.NewGuid().ToString("N"),
                Subdomain = desiredSubdomain,
                LocalPortHint = localPort,
                Session = session,
            };

            if (!_tunnelsBySubdomain.TryAdd(desiredSubdomain, tunnel))
            {
                tunnel = null;
                rejectionReason = $"Subdomain '{desiredSubdomain}' is already in use.";
                return false;
            }

            session.Tunnels[tunnel.TunnelId] = tunnel;
            rejectionReason = null;
            return true;
        }

        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var subdomain = GenerateSubdomain();
            var candidate = new Tunnel
            {
                TunnelId = Guid.NewGuid().ToString("N"),
                Subdomain = subdomain,
                LocalPortHint = localPort,
                Session = session,
            };

            if (_tunnelsBySubdomain.TryAdd(subdomain, candidate))
            {
                session.Tunnels[candidate.TunnelId] = candidate;
                tunnel = candidate;
                rejectionReason = null;
                return true;
            }
        }

        tunnel = null;
        rejectionReason = "Could not generate a unique subdomain after several attempts.";
        return false;
    }

    /// <summary>Removes one tunnel owned by the session, faulting any of its in-flight forwarded requests.</summary>
    public bool TryUnregisterTunnel(Session session, string tunnelId)
    {
        if (!session.Tunnels.TryRemove(tunnelId, out var tunnel))
        {
            return false;
        }

        _tunnelsBySubdomain.TryRemove(tunnel.Subdomain, out _);
        _streamBroker.FaultAllPending(new OperationCanceledException("Tunnel unregistered."), tunnel.PendingStreamIds.Keys.ToArray());
        return true;
    }

    public bool TryGetTunnel(string subdomain, out Tunnel tunnel) =>
        _tunnelsBySubdomain.TryGetValue(subdomain, out tunnel!);

    public Tunnel TryGetTunnel() =>
        _tunnelsBySubdomain.FirstOrDefault().Value;

    public IReadOnlyCollection<Session> ActiveSessions => _sessions.Values.ToArray();

    public bool TryGetSession(string sessionId, out Session session) =>
        _sessions.TryGetValue(sessionId, out session!);

    /// <summary>Removes the session and every tunnel it owns, failing any in-flight forwarded requests so callers don't hang.</summary>
    public void Teardown(Session session, Exception reason)
    {
        if (!_sessions.TryRemove(session.SessionId, out _))
        {
            return; // already torn down by another caller
        }

        _logger.LogInformation("Session {SessionId} torn down ({TunnelCount} tunnel(s)): {Reason}", session.SessionId, session.Tunnels.Count, reason.Message);

        session.ShutdownTokenSource.Cancel();

        foreach (var tunnel in session.Tunnels.Values)
        {
            _tunnelsBySubdomain.TryRemove(tunnel.Subdomain, out _);
            _streamBroker.FaultAllPending(reason, tunnel.PendingStreamIds.Keys.ToArray());
        }
    }

    private static string GenerateSubdomain()
    {
        Span<char> chars = stackalloc char[SubdomainLength];
        for (var i = 0; i < SubdomainLength; i++)
        {
            chars[i] = SubdomainAlphabet[RandomNumberGenerator.GetInt32(SubdomainAlphabet.Length)];
        }

        return new string(chars);
    }
}
