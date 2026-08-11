using Portix.Client.Tunneling;

namespace Portix.Client.Api;

public sealed record OpenTunnelRequest(int LocalPort, string? Subdomain);

public sealed record TunnelDto(string Id, int LocalPort, string? Subdomain, string? PublicUrl, string Status, string? LastError, bool CaptureBodiesEnabled)
{
    public static TunnelDto From(TunnelInfo tunnel) => new(
        tunnel.Id, tunnel.LocalPort, tunnel.Subdomain, tunnel.PublicUrl, tunnel.Status.ToString(), tunnel.LastError, tunnel.CaptureBodiesEnabled);
}

public static class TunnelEndpoints
{
    public static void MapTunnelEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tunnels", (TunnelManager manager) =>
            manager.List().Select(TunnelDto.From));

        app.MapPost("/api/tunnels", async (OpenTunnelRequest body, TunnelManager manager, CancellationToken ct) =>
        {
            try
            {
                var tunnel = await manager.OpenAsync(body.LocalPort, body.Subdomain, ct).ConfigureAwait(false);
                return Results.Ok(TunnelDto.From(tunnel));
            }
            catch (TunnelRegistrationRejectedException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapDelete("/api/tunnels/{id}", async (string id, TunnelManager manager, CancellationToken ct) =>
        {
            try
            {
                await manager.CloseAsync(id, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        app.MapPost("/api/tunnels/{id}/capture", (string id, bool enabled, TunnelManager manager) =>
        {
            if (!manager.TryGet(id, out var tunnel))
            {
                return Results.NotFound();
            }

            tunnel.CaptureBodiesEnabled = enabled;
            return Results.Ok(TunnelDto.From(tunnel));
        });
    }
}
