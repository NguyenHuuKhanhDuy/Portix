namespace Portix.Client.Api;

public static class SystemEndpoints
{
    public static void MapSystemEndpoints(this WebApplication app)
    {
        // Loopback-only, no auth — same trust model as every other local endpoint here. Lets
        // `portix update` ask a running daemon to stop before it replaces the executable on disk.
        app.MapPost("/api/shutdown", (IHostApplicationLifetime lifetime) =>
        {
            lifetime.StopApplication();
            return Results.Ok();
        });
    }
}
