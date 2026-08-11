using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Portix.Client.Inspector;
using Portix.Shared.Protocol;

namespace Portix.Client.Tunneling;

/// <summary>
/// Handles one /data/{streamId} connection: resolves which tunnel it belongs to, reads the raw
/// public request the server forwards, replays it against that tunnel's local port, streams
/// the local app's response back, and captures the exchange for the traffic inspector.
/// </summary>
public sealed class RequestForwarder
{
    private const int BodyPreviewCapBytes = 100 * 1024;
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(100);

    private static readonly HashSet<string> ContentHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type", "Content-Disposition", "Content-Encoding",
        "Content-Language", "Content-Location", "Content-MD5", "Content-Range",
        "Last-Modified", "Expires",
    };

    private readonly HttpClient _localClient;
    private readonly Uri _serverBaseUri;
    private readonly TunnelManager _tunnelManager;
    private readonly RequestStore _requestStore;
    private readonly ILogger<RequestForwarder> _logger;

    public RequestForwarder(
        HttpClient localClient,
        Uri serverBaseUri,
        TunnelManager tunnelManager,
        RequestStore requestStore,
        ILogger<RequestForwarder> logger)
    {
        _localClient = localClient;
        _serverBaseUri = serverBaseUri;
        _tunnelManager = tunnelManager;
        _requestStore = requestStore;
        _logger = logger;
    }

    /// <summary>
    /// <paramref name="serverClient"/> must be the same HttpClient instance backing the control
    /// channel this stream signal arrived on — each reconnect gets its own fresh HttpClient (see
    /// ControlChannelBackgroundService) so a dead pooled HTTP/2 connection from a previous server
    /// process is never reused for a new one.
    /// </summary>
    public async Task HandleStreamAsync(string streamId, string tunnelId, HttpClient serverClient, CancellationToken ct)
    {
        if (!_tunnelManager.TryGet(tunnelId, out var tunnel))
        {
            _logger.LogWarning("Received stream {StreamId} for unknown tunnel {TunnelId}; not forwarding.", streamId, tunnelId);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var timestamp = DateTimeOffset.UtcNow;
        var captureBodies = tunnel.CaptureBodiesEnabled;

        using var duplex = new DuplexRequestChannel();
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_serverBaseUri, $"/data/{streamId}"))
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = duplex,
        };

        try
        {
            var responseTask = serverClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var requestStream = await duplex.WaitForWriteStreamAsync(responseTask, ct).ConfigureAwait(false);

            using var response = await responseTask.ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var serverStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

            var requestHeader = await DataMessageCodec.ReadHeaderAsync(serverStream, ct).ConfigureAwait(false);

            var requestTee = captureBodies ? new CappedTeeStream(serverStream, BodyPreviewCapBytes) : null;

            using var localRequest = new HttpRequestMessage(
                new HttpMethod(requestHeader.Method ?? "GET"),
                new Uri($"http://localhost:{tunnel.LocalPort}{requestHeader.Path}"))
            {
                Content = new StreamContent((Stream?)requestTee ?? serverStream),
            };

            foreach (var (name, values) in requestHeader.Headers)
            {
                if (ContentHeaderNames.Contains(name))
                {
                    localRequest.Content.Headers.TryAddWithoutValidation(name, values);
                }
                else
                {
                    localRequest.Headers.TryAddWithoutValidation(name, values);
                }
            }

            using var localResponse = await SendWithIdleTimeoutAsync(localRequest, tunnel.LocalPort, ct).ConfigureAwait(false);
            var localResponseStream = await localResponse.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var responseTee = captureBodies ? new CappedTeeStream(localResponseStream, BodyPreviewCapBytes) : null;

            var responseHeaders = MergeResponseHeaders(localResponse);
            var responseHeader = new DataMessageHeader
            {
                StatusCode = (int)localResponse.StatusCode,
                Headers = responseHeaders,
            };

            await DataMessageCodec.WriteHeaderAsync(requestStream, responseHeader, ct).ConfigureAwait(false);
            await CopyWithIdleTimeoutAsync((Stream?)responseTee ?? localResponseStream, requestStream, ct).ConfigureAwait(false);

            Capture(tunnelId, requestHeader, requestTee, (int)localResponse.StatusCode, responseHeaders, responseTee, timestamp, stopwatch.Elapsed.TotalMilliseconds);
        }
        finally
        {
            duplex.CompleteWriting();
        }
    }

    private void Capture(
        string tunnelId,
        DataMessageHeader requestHeader,
        CappedTeeStream? requestTee,
        int statusCode,
        Dictionary<string, List<string>> responseHeaders,
        CappedTeeStream? responseTee,
        DateTimeOffset timestamp,
        double durationMs)
    {
        _requestStore.Add(tunnelId, sequence => new CapturedRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            TunnelId = tunnelId,
            Method = requestHeader.Method ?? "GET",
            Path = requestHeader.Path ?? "/",
            RequestHeaders = Flatten(requestHeader.Headers),
            RequestBodyPreview = requestTee is null ? null : requestTee.Preview.ToArray(),
            RequestBodyTruncated = requestTee?.Truncated ?? false,
            StatusCode = statusCode,
            ResponseHeaders = Flatten(responseHeaders),
            ResponseBodyPreview = responseTee is null ? null : responseTee.Preview.ToArray(),
            ResponseBodyTruncated = responseTee?.Truncated ?? false,
            DurationMs = durationMs,
            Timestamp = timestamp,
            SourceIp = requestHeader.SourceIp,
        });
    }

    private static Dictionary<string, string> Flatten(Dictionary<string, List<string>> headers)
    {
        var result = new Dictionary<string, string>();
        foreach (var (name, values) in headers)
        {
            result[name] = string.Join(", ", values);
        }

        return result;
    }

    private async Task<HttpResponseMessage> SendWithIdleTimeoutAsync(HttpRequestMessage request, int localPort, CancellationToken ct)
    {
        using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idleCts.CancelAfter(IdleTimeout);
        try
        {
            return await _localClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, idleCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Local app on port {localPort} did not respond in time.");
        }
    }

    private static Dictionary<string, List<string>> MergeResponseHeaders(HttpResponseMessage response)
    {
        var result = new Dictionary<string, List<string>>();
        foreach (var header in response.Headers)
        {
            if (!HopByHopHeaders.Names.Contains(header.Key))
            {
                result[header.Key] = header.Value.ToList();
            }
        }

        foreach (var header in response.Content.Headers)
        {
            if (!HopByHopHeaders.Names.Contains(header.Key))
            {
                result[header.Key] = header.Value.ToList();
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
                throw new TimeoutException("Idle timeout waiting for local app response data.");
            }

            if (read == 0)
            {
                return;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }
    }
}
