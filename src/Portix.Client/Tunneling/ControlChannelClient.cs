using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using Portix.Shared.Protocol;

namespace Portix.Client.Tunneling;

public sealed class HandshakeResult
{
    public required string SessionId { get; init; }
    public required int PublicPort { get; init; }
    public required string PublicHostSuffix { get; init; }
}

public sealed class TunnelRegisteredResult
{
    public required string TunnelId { get; init; }
    public required string Subdomain { get; init; }
    public required string PublicUrl { get; init; }
}

/// <summary>Server rejected a RegisterTunnel or the tunnel could not be reached for other reasons.</summary>
public sealed class TunnelRegistrationRejectedException : Exception
{
    public TunnelRegistrationRejectedException(string reason) : base(reason)
    {
    }
}

/// <summary>
/// One long-lived POST /control connection: session handshake, periodic heartbeat, tunnel
/// register/unregister (with reply correlation), and the read loop that dispatches NEW_STREAM
/// signals to the caller.
/// </summary>
public sealed class ControlChannelClient : IAsyncDisposable
{
    // Kestrel doesn't reliably flush a pushed control message (e.g. NEW_STREAM) to the wire
    // until the connection sees more traffic in the other direction; a frequent heartbeat is
    // what keeps that latency low, not just a liveness check. Measured empirically: with a
    // 15s interval, NEW_STREAM delivery lagged by up to ~15s; at 1s it's sub-second.
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _serverClient;
    private readonly Uri _controlUri;
    private readonly string _token;
    private readonly DuplexRequestChannel _duplex = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ControlMessage>> _pendingRequests = new();

    private HttpResponseMessage? _response;
    private Stream? _responseStream;
    private Stream? _requestStream;

    public ControlChannelClient(HttpClient serverClient, Uri serverBaseUri, string token)
    {
        _serverClient = serverClient;
        _controlUri = new Uri(serverBaseUri, "/control");
        _token = token;
    }

    public async Task<HandshakeResult> ConnectAsync(CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _controlUri)
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = _duplex,
        };

        var responseTask = _serverClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        _requestStream = await _duplex.WaitForWriteStreamAsync(responseTask, ct).ConfigureAwait(false);
        await FrameCodec.WriteMessageAsync(_requestStream, ControlMessage.CreateHello(_token), ct).ConfigureAwait(false);

        _response = await responseTask.ConfigureAwait(false);
        _response.EnsureSuccessStatusCode();
        _responseStream = await _response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        var ack = await FrameCodec.ReadMessageAsync(_responseStream, ct).ConfigureAwait(false);
        if (ack is null)
        {
            throw new IOException("Control channel closed before handshake completed.");
        }

        if (ack.Type == ControlMessageType.HelloReject)
        {
            throw new InvalidOperationException($"Server rejected handshake: {ack.Reason}");
        }

        if (ack.Type != ControlMessageType.HelloAck || ack.SessionId is null || ack.PublicPort is null)
        {
            throw new InvalidOperationException("Malformed handshake response from server.");
        }

        return new HandshakeResult
        {
            SessionId = ack.SessionId,
            PublicPort = ack.PublicPort.Value,
            PublicHostSuffix = ack.PublicHostSuffix ?? "localtest.me",
        };
    }

    /// <summary>Runs the heartbeat sender and the NEW_STREAM read loop until the channel breaks or <paramref name="ct"/> is cancelled.</summary>
    public async Task RunAsync(Func<string, string, Task> onNewStream, CancellationToken ct)
    {
        var readLoop = ReadLoopAsync(onNewStream, ct);
        var heartbeatLoop = HeartbeatLoopAsync(ct);
        await Task.WhenAll(readLoop, heartbeatLoop).ConfigureAwait(false);
    }

    public async Task<TunnelRegisteredResult> RegisterTunnelAsync(int localPort, string? desiredSubdomain, CancellationToken ct)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<ControlMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = tcs;

        try
        {
            await WriteAsync(ControlMessage.CreateRegisterTunnel(requestId, localPort, desiredSubdomain), ct).ConfigureAwait(false);
            var reply = await WaitForReplyAsync(tcs, ct).ConfigureAwait(false);

            if (reply.Type == ControlMessageType.TunnelRegisterRejected)
            {
                throw new TunnelRegistrationRejectedException(reply.Reason ?? "Tunnel registration was rejected.");
            }

            if (reply.Type != ControlMessageType.TunnelRegistered || reply.TunnelId is null || reply.Subdomain is null || reply.PublicUrl is null)
            {
                throw new InvalidOperationException("Malformed tunnel registration response from server.");
            }

            return new TunnelRegisteredResult { TunnelId = reply.TunnelId, Subdomain = reply.Subdomain, PublicUrl = reply.PublicUrl };
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    public async Task UnregisterTunnelAsync(string tunnelId, CancellationToken ct)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<ControlMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = tcs;

        try
        {
            await WriteAsync(ControlMessage.CreateUnregisterTunnel(requestId, tunnelId), ct).ConfigureAwait(false);
            await WaitForReplyAsync(tcs, ct).ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    private static async Task<ControlMessage> WaitForReplyAsync(TaskCompletionSource<ControlMessage> tcs, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(RequestTimeout);
        try
        {
            return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("Server did not reply in time.");
        }
    }

    private async Task ReadLoopAsync(Func<string, string, Task> onNewStream, CancellationToken ct)
    {
        while (true)
        {
            var message = await FrameCodec.ReadMessageAsync(_responseStream!, ct).ConfigureAwait(false);
            if (message is null)
            {
                throw new IOException("Control channel closed by server.");
            }

            switch (message.Type)
            {
                case ControlMessageType.NewStream when message.StreamId is not null && message.TunnelId is not null:
                    _ = onNewStream(message.StreamId, message.TunnelId); // forwarding proceeds independently; a slow local app must not stall the control loop
                    break;

                case ControlMessageType.TunnelRegistered:
                case ControlMessageType.TunnelRegisterRejected:
                case ControlMessageType.TunnelUnregistered:
                    if (message.RequestId is not null && _pendingRequests.TryRemove(message.RequestId, out var tcs))
                    {
                        tcs.TrySetResult(message);
                    }
                    break;
            }
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(HeartbeatInterval);
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            await WriteAsync(ControlMessage.CreatePing(), ct).ConfigureAwait(false);
        }
    }

    private async Task WriteAsync(ControlMessage message, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await FrameCodec.WriteMessageAsync(_requestStream!, message, ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        foreach (var tcs in _pendingRequests.Values)
        {
            tcs.TrySetCanceled();
        }

        _duplex.CompleteWriting();
        _duplex.Dispose();
        _response?.Dispose();
        return ValueTask.CompletedTask;
    }
}
