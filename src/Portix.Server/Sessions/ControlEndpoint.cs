using Microsoft.AspNetCore.Http.Features;
using Portix.Server.Auth;
using Portix.Server.Forwarding;
using Portix.Shared.Protocol;

namespace Portix.Server.Sessions;

/// <summary>Handles the long-lived POST /control connection: handshake, heartbeat, tunnel register/unregister, and NEW_STREAM push.</summary>
public sealed class ControlEndpoint
{
    private readonly SessionRegistry _registry;
    private readonly ILogger<ControlEndpoint> _logger;
    private readonly int _publicPort;
    private readonly string _publicHostSuffix;
    private readonly string _publicUrlScheme;
    private readonly int? _publicUrlPort;

    public ControlEndpoint(SessionRegistry registry, ILogger<ControlEndpoint> logger, IConfiguration configuration)
    {
        _registry = registry;
        _logger = logger;
        _publicPort = configuration.GetValue("Portix:PublicPort", 8080);
        _publicHostSuffix = configuration["Portix:PublicHostSuffix"] ?? "localtest.me";
        _publicUrlScheme = configuration["Portix:PublicUrlScheme"] ?? "http";
        _publicUrlPort = configuration.GetValue<int?>("Portix:PublicUrlPort");
    }

    // AuthService is scoped (it holds a DbContext) and is resolved per-request by minimal-API model
    // binding, rather than constructor-injected into this singleton — avoids capturing a scoped
    // dependency for the lifetime of the process.
    public async Task HandleAsync(HttpContext context, AuthService authService)
    {
        // The control channel is a long-lived connection that is silent between heartbeats;
        // Kestrel's slow-loris protection would otherwise kill it after a few idle seconds.
        context.DisableMinDataRateLimits();

        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/octet-stream";
        await context.Response.StartAsync(context.RequestAborted).ConfigureAwait(false);

        var requestBody = context.Request.Body;
        var responseBody = context.Response.Body;

        var hello = await FrameCodec.ReadMessageAsync(requestBody, context.RequestAborted).ConfigureAwait(false);
        if (hello is null || hello.Type != ControlMessageType.Hello)
        {
            await FrameCodec.WriteMessageAsync(responseBody, ControlMessage.CreateHelloReject("Malformed handshake."), context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var owner = await authService.AuthenticateAsync(hello.Token, context.RequestAborted).ConfigureAwait(false);
        if (owner is null)
        {
            _logger.LogWarning("Rejected control handshake: invalid token.");
            await FrameCodec.WriteMessageAsync(responseBody, ControlMessage.CreateHelloReject("Invalid token."), context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var session = _registry.CreateSession(responseBody, owner);
        _logger.LogInformation("Session {SessionId} established for user {UserId}.", session.SessionId, owner.Id);

        await FrameCodec.WriteMessageAsync(
            responseBody,
            ControlMessage.CreateHelloAck(session.SessionId, _publicPort, _publicHostSuffix),
            context.RequestAborted).ConfigureAwait(false);

        try
        {
            await RunMessageLoopAsync(session, requestBody, context.RequestAborted).ConfigureAwait(false);
            _registry.Teardown(session, new OperationCanceledException("Control channel closed by client."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _registry.Teardown(session, ex);
        }
        finally
        {
            if (context.RequestAborted.IsCancellationRequested)
            {
                _registry.Teardown(session, new OperationCanceledException("Server shutting down or connection aborted."));
            }
        }
    }

    private async Task RunMessageLoopAsync(Session session, Stream requestBody, CancellationToken ct)
    {
        while (true)
        {
            var message = await FrameCodec.ReadMessageAsync(requestBody, ct).ConfigureAwait(false);
            if (message is null)
            {
                return; // client closed the control channel cleanly
            }

            switch (message.Type)
            {
                case ControlMessageType.Ping:
                    session.LastHeartbeatUtc = DateTimeOffset.UtcNow;
                    await session.WriteControlMessageAsync(ControlMessage.CreatePong(), ct).ConfigureAwait(false);
                    break;

                case ControlMessageType.RegisterTunnel when message.RequestId is not null && message.LocalPort is not null:
                    await HandleRegisterTunnelAsync(session, message, ct).ConfigureAwait(false);
                    break;

                case ControlMessageType.UnregisterTunnel when message.RequestId is not null && message.TunnelId is not null:
                    _registry.TryUnregisterTunnel(session, message.TunnelId);
                    await session.WriteControlMessageAsync(ControlMessage.CreateTunnelUnregistered(message.RequestId, message.TunnelId), ct).ConfigureAwait(false);
                    break;

                default:
                    _logger.LogWarning("Session {SessionId} sent unrecognized/malformed message type {Type}.", session.SessionId, message.Type);
                    break;
            }
        }
    }

    private async Task HandleRegisterTunnelAsync(Session session, ControlMessage message, CancellationToken ct)
    {
        var registered = _registry.TryRegisterTunnel(session, message.LocalPort!.Value, message.DesiredSubdomain, out var tunnel, out var rejectionReason);

        ControlMessage reply;
        if (registered && tunnel is not null)
        {
            var publicUrl = BuildPublicUrl(tunnel.Subdomain);
            _logger.LogInformation("Session {SessionId} registered tunnel {TunnelId}: {Subdomain} -> localhost:{Port}", session.SessionId, tunnel.TunnelId, tunnel.Subdomain, tunnel.LocalPortHint);
            reply = ControlMessage.CreateTunnelRegistered(message.RequestId!, tunnel.TunnelId, tunnel.Subdomain, publicUrl);
        }
        else
        {
            reply = ControlMessage.CreateTunnelRegisterRejected(message.RequestId!, rejectionReason ?? "Registration failed.");
        }

        await session.WriteControlMessageAsync(reply, ct).ConfigureAwait(false);
    }

    // Advertised scheme/port are independent of _publicPort (the internal Kestrel bind port) so a
    // reverse proxy fronting the server on a different scheme/port (e.g. https on 443) can be
    // advertised correctly without changing what Kestrel itself listens on.
    private string BuildPublicUrl(string subdomain)
    {
        int? displayPort = _publicUrlPort ?? (_publicUrlScheme == "http" ? _publicPort : null);
        var isConventionalPort = displayPort is null
            || (_publicUrlScheme == "http" && displayPort == 80)
            || (_publicUrlScheme == "https" && displayPort == 443);
        var portSuffix = isConventionalPort ? "" : $":{displayPort}";
        return $"{_publicUrlScheme}://{subdomain}.{_publicHostSuffix}{portSuffix}";
    }
}
