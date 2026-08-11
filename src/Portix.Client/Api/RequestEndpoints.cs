using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Portix.Client.Inspector;
using Portix.Client.Tunneling;

namespace Portix.Client.Api;

public sealed record CapturedRequestSummaryDto(string Id, string TunnelId, string Method, string Path, int StatusCode, double DurationMs, DateTimeOffset Timestamp, string? SourceIp)
{
    public static CapturedRequestSummaryDto From(CapturedRequest c) => new(c.Id, c.TunnelId, c.Method, c.Path, c.StatusCode, c.DurationMs, c.Timestamp, c.SourceIp);
}

public sealed record CapturedRequestDetailDto(
    string Id, string TunnelId, string Method, string Path,
    Dictionary<string, string> RequestHeaders, string? RequestBodyPreviewBase64, bool RequestBodyTruncated,
    int StatusCode, Dictionary<string, string> ResponseHeaders, string? ResponseBodyPreviewBase64, bool ResponseBodyTruncated,
    double DurationMs, DateTimeOffset Timestamp, string? SourceIp)
{
    public static CapturedRequestDetailDto From(CapturedRequest c) => new(
        c.Id, c.TunnelId, c.Method, c.Path,
        c.RequestHeaders, c.RequestBodyPreview is null ? null : Convert.ToBase64String(c.RequestBodyPreview), c.RequestBodyTruncated,
        c.StatusCode, c.ResponseHeaders, c.ResponseBodyPreview is null ? null : Convert.ToBase64String(c.ResponseBodyPreview), c.ResponseBodyTruncated,
        c.DurationMs, c.Timestamp, c.SourceIp);
}

public sealed record ReplayResultDto(int StatusCode, Dictionary<string, string> Headers, string BodyBase64);

public static class RequestEndpoints
{
    public static void MapRequestEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tunnels/{id}/requests", (string id, RequestStore store, int? limit) =>
            store.List(id, limit ?? 100).Select(CapturedRequestSummaryDto.From));

        app.MapGet("/api/tunnels/{id}/requests/{reqId}", (string id, string reqId, RequestStore store) =>
            store.TryGet(id, reqId, out var capture)
                ? Results.Ok(CapturedRequestDetailDto.From(capture))
                : Results.NotFound());

        app.MapDelete("/api/tunnels/{id}/requests", (string id, RequestStore store) =>
        {
            store.Clear(id);
            return Results.NoContent();
        });

        app.MapPost("/api/tunnels/{id}/requests/{reqId}/replay", async (
            string id, string reqId, TunnelManager manager, RequestStore store,
            [FromKeyedServices("local")] HttpClient localClient, CancellationToken ct) =>
        {
            if (!manager.TryGet(id, out var tunnel))
            {
                return Results.NotFound(new { error = "Tunnel not found." });
            }

            if (!store.TryGet(id, reqId, out var capture))
            {
                return Results.NotFound(new { error = "Captured request not found." });
            }

            using var request = new HttpRequestMessage(new HttpMethod(capture.Method), $"http://localhost:{tunnel.LocalPort}{capture.Path}");
            if (capture.RequestBodyPreview is { Length: > 0 } body)
            {
                request.Content = new ByteArrayContent(body);
            }

            foreach (var (name, value) in capture.RequestHeaders)
            {
                if (request.Content is not null && name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                {
                    request.Content.Headers.TryAddWithoutValidation(name, value);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(name, value);
                }
            }

            using var response = await localClient.SendAsync(request, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

            var headers = new Dictionary<string, string>();
            foreach (var h in response.Headers)
            {
                headers[h.Key] = string.Join(", ", h.Value);
            }

            return Results.Ok(new ReplayResultDto((int)response.StatusCode, headers, Convert.ToBase64String(responseBody)));
        });
    }
}
