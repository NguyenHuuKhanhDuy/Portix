using Microsoft.AspNetCore.Server.Kestrel.Core.Features;

namespace Portix.Server.Forwarding;

public static class HttpContextExtensions
{
    /// <summary>
    /// Turns off Kestrel's slow-loris protection for this connection. Our tunnel connections
    /// are legitimately silent for long stretches (control channel between heartbeats) or
    /// governed by our own explicit idle-read timeouts (data channel), so Kestrel's default
    /// minimum-bytes-per-second heuristic would otherwise kill them prematurely.
    /// </summary>
    public static void DisableMinDataRateLimits(this HttpContext context)
    {
        var minRequestRate = context.Features.Get<IHttpMinRequestBodyDataRateFeature>();
        if (minRequestRate is not null)
        {
            minRequestRate.MinDataRate = null;
        }

        var minResponseRate = context.Features.Get<IHttpMinResponseDataRateFeature>();
        if (minResponseRate is not null)
        {
            minResponseRate.MinDataRate = null;
        }
    }
}
