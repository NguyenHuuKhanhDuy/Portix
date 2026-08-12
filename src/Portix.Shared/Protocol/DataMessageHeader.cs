namespace Portix.Shared.Protocol;

/// <summary>
/// Header block sent at the start of a /data/{streamId} leg, describing either
/// an HTTP request (Method+Path set) or an HTTP response (StatusCode set).
/// Raw body bytes follow immediately after the header and run to end-of-stream,
/// since each leg carries exactly one HTTP message.
/// </summary>
public sealed class DataMessageHeader
{
    public string? Method { get; set; }
    public string? Path { get; set; }
    public int? StatusCode { get; set; }
    public Dictionary<string, List<string>> Headers { get; set; } = new();

    /// <summary>Request legs only: the public caller's source IP, for the traffic inspector.</summary>
    public string? SourceIp { get; set; }

    /// <summary>
    /// Response legs only: set by the client instead of a real StatusCode/body when it could not
    /// reach the local app at all (e.g. connection refused, response timeout). A short machine
    /// code such as "local_connection_failed" or "local_response_timeout".
    /// </summary>
    public string? GatewayErrorReason { get; set; }

    /// <summary>Response legs only: human-readable detail accompanying <see cref="GatewayErrorReason"/>.</summary>
    public string? GatewayErrorDetail { get; set; }
}
