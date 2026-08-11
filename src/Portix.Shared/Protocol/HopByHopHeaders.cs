namespace Portix.Shared.Protocol;

/// <summary>
/// Headers that describe framing/connection state for one specific hop and must not be
/// blindly replayed onto the next hop (their new hop recomputes its own framing).
/// </summary>
public static class HopByHopHeaders
{
    public static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host",
        "Content-Length",
        "Transfer-Encoding",
        "Connection",
        "Keep-Alive",
        "Proxy-Connection",
        "Upgrade",
        "TE",
        "Trailer",
    };
}
