using Microsoft.AspNetCore.Http.Features;
using Portix.Server.Sessions;
using Portix.Shared.Protocol;

namespace Portix.Server.Forwarding;

/// <summary>
/// Handles every request on the public listener: resolves the subdomain to a session,
/// signals the client to open a /data/{streamId} connection, then relays the request
/// and response bytes through it in both directions concurrently.
/// </summary>
public sealed class PublicForwardingMiddleware
{
    private static readonly TimeSpan StreamOpenTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(100);

    private readonly SessionRegistry _registry;
    private readonly StreamBroker _broker;
    private readonly ILogger<PublicForwardingMiddleware> _logger;

    public PublicForwardingMiddleware(SessionRegistry registry, StreamBroker broker, ILogger<PublicForwardingMiddleware> logger)
    {
        _registry = registry;
        _broker = broker;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        // Idle time is governed by CopyWithIdleTimeoutAsync below, not Kestrel's slow-loris default,
        // since a legitimately slow (but alive) upload/download must not be cut off early.
        context.DisableMinDataRateLimits();

        var subdomain = context.Request.Host.Host.Split('.')[0];

        if (!_registry.TryGetTunnel(subdomain, out var tunnel))
        {
            await WriteGatewayErrorAsync(
                context,
                StatusCodes.Status502BadGateway,
                "Tunnel not found",
                $"No local server is currently exposed at '{subdomain}'.",
                detail: null).ConfigureAwait(false);
            return;
        }
        
        var session = tunnel.Session;
        var streamId = Guid.NewGuid().ToString("N");
        tunnel.PendingStreamIds.TryAdd(streamId, 0);

        using var openCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, session.ShutdownTokenSource.Token);
        openCts.CancelAfter(StreamOpenTimeout);
        var waitForChannel = _broker.RegisterAndWait(streamId, openCts.Token);

        await session.WriteControlMessageAsync(ControlMessage.CreateNewStream(streamId, tunnel.TunnelId), context.RequestAborted).ConfigureAwait(false);

        DataChannel channel;
        try
        {
            channel = await waitForChannel.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            tunnel.PendingStreamIds.TryRemove(streamId, out _);
            await WriteGatewayErrorAsync(
                context,
                StatusCodes.Status504GatewayTimeout,
                "Local agent unreachable",
                "The Portix agent for this tunnel didn't respond in time. Is it still running?",
                detail: null).ConfigureAwait(false);
            return;
        }

        tunnel.PendingStreamIds.TryRemove(streamId, out _);

        var isUpgrade = UpgradeRequestDetector.IsUpgrade(context.Request.Headers["Connection"], context.Request.Headers["Upgrade"]);

        using var exchangeCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, session.ShutdownTokenSource.Token);
        try
        {
            if (isUpgrade)
            {
                await HandleUpgradeAsync(context, channel, exchangeCts.Token).ConfigureAwait(false);
            }
            else
            {
                var writeTask = WritePublicRequestAsync(context, channel, exchangeCts.Token);
                var readTask = ReadLocalResponseAsync(context, channel, exchangeCts.Token);
                await Task.WhenAll(writeTask, readTask).ConfigureAwait(false);
            }

            channel.Completion.TrySetResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error relaying request for stream {StreamId}", streamId);
            channel.Completion.TrySetException(ex);
            if (!context.Response.HasStarted)
            {
                await WriteGatewayErrorAsync(
                    context,
                    StatusCodes.Status502BadGateway,
                    "Local server unreachable",
                    "The request could not be relayed to your local server.",
                    detail: ex.Message).ConfigureAwait(false);
            }
        }
    }

    private static Task WriteGatewayErrorAsync(HttpContext context, int statusCode, string title, string reason, string? detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/html; charset=utf-8";
        return context.Response.WriteAsync(GatewayErrorPage.Render(title, reason, detail));
    }

    private static async Task WritePublicRequestAsync(HttpContext context, DataChannel channel, CancellationToken ct)
    {
        var header = new DataMessageHeader
        {
            Method = context.Request.Method,
            Path = context.Request.Path.ToString() + context.Request.QueryString.ToString(),
            Headers = ToHeaderDictionary(context.Request.Headers),
            SourceIp = context.Connection.RemoteIpAddress?.ToString(),
        };

        await DataMessageCodec.WriteHeaderAsync(channel.WriteToClient, header, ct).ConfigureAwait(false);
        await CopyWithIdleTimeoutAsync(context.Request.Body, channel.WriteToClient, ct).ConfigureAwait(false);
        await channel.CompleteWriteToClientAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Handles a request that is itself a protocol-upgrade handshake (e.g. a WebSocket).
    /// The initial request/response headers still use the ordinary DataMessageHeader exchange;
    /// once the local app answers 101, this switches to a raw, unframed byte pump between the
    /// public caller's upgraded connection and the local app's, for as long as it stays open.
    /// </summary>
    private static async Task HandleUpgradeAsync(HttpContext context, DataChannel channel, CancellationToken ct)
    {
        var requestHeader = new DataMessageHeader
        {
            Method = context.Request.Method,
            Path = context.Request.Path.ToString() + context.Request.QueryString.ToString(),
            Headers = ToHeaderDictionary(context.Request.Headers, preserveConnectionUpgrade: true),
            SourceIp = context.Connection.RemoteIpAddress?.ToString(),
        };
        await DataMessageCodec.WriteHeaderAsync(channel.WriteToClient, requestHeader, ct).ConfigureAwait(false);

        var responseHeader = await DataMessageCodec.ReadHeaderAsync(channel.ReadFromClient, ct).ConfigureAwait(false);

        if (responseHeader.GatewayErrorReason is not null)
        {
            await WriteGatewayErrorAsync(
                context,
                StatusCodes.Status502BadGateway,
                "Local server unreachable",
                responseHeader.GatewayErrorDetail ?? "Your local server could not be reached.",
                detail: responseHeader.GatewayErrorReason).ConfigureAwait(false);
            return;
        }

        if (responseHeader.StatusCode != StatusCodes.Status101SwitchingProtocols)
        {
            // The local app declined the upgrade — relay whatever it actually answered, same as any other response.
            context.Response.StatusCode = responseHeader.StatusCode ?? StatusCodes.Status502BadGateway;
            foreach (var (name, values) in responseHeader.Headers)
            {
                if (!HopByHopHeaders.Names.Contains(name))
                {
                    context.Response.Headers[name] = values.ToArray();
                }
            }

            await CopyWithIdleTimeoutAsync(channel.ReadFromClient, context.Response.Body, ct).ConfigureAwait(false);
            return;
        }

        foreach (var (name, values) in responseHeader.Headers)
        {
            var isConnectionOrUpgrade = name.Equals("Connection", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase);

            if (!HopByHopHeaders.Names.Contains(name) || isConnectionOrUpgrade)
            {
                context.Response.Headers[name] = values.ToArray();
            }
        }
        context.Response.StatusCode = StatusCodes.Status101SwitchingProtocols;

        var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>()
            ?? throw new InvalidOperationException("This connection does not support protocol upgrades.");
        var publicStream = await upgradeFeature.UpgradeAsync().ConfigureAwait(false);

        await DuplexPump.RunAsync(publicStream, publicStream, channel.ReadFromClient, channel.WriteToClient, ct).ConfigureAwait(false);
        await channel.CompleteWriteToClientAsync().ConfigureAwait(false);
    }

    private static async Task ReadLocalResponseAsync(HttpContext context, DataChannel channel, CancellationToken ct)
    {
        var header = await DataMessageCodec.ReadHeaderAsync(channel.ReadFromClient, ct).ConfigureAwait(false);

        if (header.GatewayErrorReason is not null)
        {
            await WriteGatewayErrorAsync(
                context,
                StatusCodes.Status502BadGateway,
                "Local server unreachable",
                header.GatewayErrorDetail ?? "Your local server could not be reached.",
                detail: header.GatewayErrorReason).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = header.StatusCode ?? StatusCodes.Status502BadGateway;
        foreach (var (name, values) in header.Headers)
        {
            if (!HopByHopHeaders.Names.Contains(name))
            {
                context.Response.Headers[name] = values.ToArray();
            }
        }

        await CopyWithIdleTimeoutAsync(channel.ReadFromClient, context.Response.Body, ct).ConfigureAwait(false);
    }

    private static Dictionary<string, List<string>> ToHeaderDictionary(IHeaderDictionary headers, bool preserveConnectionUpgrade = false)
    {
        var result = new Dictionary<string, List<string>>();
        foreach (var header in headers)
        {
            var isConnectionOrUpgrade = preserveConnectionUpgrade
                && (header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Upgrade", StringComparison.OrdinalIgnoreCase));

            if (!HopByHopHeaders.Names.Contains(header.Key) || isConnectionOrUpgrade)
            {
                result[header.Key] = header.Value.Select(v => v ?? string.Empty).ToList();
            }
        }

        return result;
    }

    private static async Task CopyWithIdleTimeoutAsync(Stream source, Stream destination, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        // One reused linked source for the whole copy, not one per chunk: CancelAfter reschedules
        // the same pending deadline on each iteration instead of allocating a new source + timer
        // per 16 KB read, which otherwise happens hundreds of times for a large body.
        using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        while (true)
        {
            idleCts.CancelAfter(IdleTimeout);

            int read;
            try
            {
                read = await source.ReadAsync(buffer, idleCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException("Idle timeout waiting for data.");
            }

            if (read == 0)
            {
                return;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }
    }
}
