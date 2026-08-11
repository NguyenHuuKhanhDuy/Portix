namespace Portix.Client.Inspector;

/// <summary>One forwarded request/response exchange, captured for the traffic inspector.</summary>
public sealed class CapturedRequest
{
    public required string Id { get; init; }
    public required string TunnelId { get; init; }

    public required string Method { get; init; }
    public required string Path { get; init; }
    public required Dictionary<string, string> RequestHeaders { get; init; }
    public byte[]? RequestBodyPreview { get; init; }
    public bool RequestBodyTruncated { get; init; }

    public required int StatusCode { get; init; }
    public required Dictionary<string, string> ResponseHeaders { get; init; }
    public byte[]? ResponseBodyPreview { get; init; }
    public bool ResponseBodyTruncated { get; init; }

    public required double DurationMs { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string? SourceIp { get; init; }

    /// <summary>Approximate memory this capture holds, for the global cap — body previews dominate.</summary>
    public int ApproxSizeBytes => (RequestBodyPreview?.Length ?? 0) + (ResponseBodyPreview?.Length ?? 0) + 256;
}
