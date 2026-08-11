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
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsync($"No active tunnel for '{subdomain}'.").ConfigureAwait(false);
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
            context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            await context.Response.WriteAsync("Client did not open the data stream in time.").ConfigureAwait(false);
            return;
        }

        tunnel.PendingStreamIds.TryRemove(streamId, out _);

        using var exchangeCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, session.ShutdownTokenSource.Token);
        try
        {
            var writeTask = WritePublicRequestAsync(context, channel, exchangeCts.Token);
            var readTask = ReadLocalResponseAsync(context, channel, exchangeCts.Token);
            await Task.WhenAll(writeTask, readTask).ConfigureAwait(false);
            channel.Completion.TrySetResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error relaying request for stream {StreamId}", streamId);
            channel.Completion.TrySetException(ex);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
            }
        }
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

    private static async Task ReadLocalResponseAsync(HttpContext context, DataChannel channel, CancellationToken ct)
    {
        var header = await DataMessageCodec.ReadHeaderAsync(channel.ReadFromClient, ct).ConfigureAwait(false);

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

    private static Dictionary<string, List<string>> ToHeaderDictionary(IHeaderDictionary headers)
    {
        var result = new Dictionary<string, List<string>>();
        foreach (var header in headers)
        {
            if (!HopByHopHeaders.Names.Contains(header.Key))
            {
                result[header.Key] = header.Value.Select(v => v ?? string.Empty).ToList();
            }
        }

        return result;
    }

    private static async Task CopyWithIdleTimeoutAsync(Stream source, Stream destination, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        while (true)
        {
            using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
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
