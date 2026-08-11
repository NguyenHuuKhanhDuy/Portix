using Microsoft.AspNetCore.Http.Features;

namespace Portix.Server.Forwarding;

/// <summary>Handles POST /data/{streamId}, the connection a client opens in response to a NEW_STREAM signal.</summary>
public sealed class DataEndpoint
{
    private readonly StreamBroker _broker;
    private readonly ILogger<DataEndpoint> _logger;

    public DataEndpoint(StreamBroker broker, ILogger<DataEndpoint> logger)
    {
        _broker = broker;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context, string streamId)
    {
        // Idle time here is governed by PublicForwardingMiddleware's own idle-read timeout, not Kestrel's default.
        context.DisableMinDataRateLimits();

        var channel = new DataChannel
        {
            ReadFromClient = context.Request.Body,
            WriteToClient = context.Response.Body,
            CompleteWriteToClientAsync = () => context.Response.CompleteAsync(),
        };

        if (!_broker.Complete(streamId, channel))
        {
            _logger.LogWarning("Received /data/{StreamId} with no matching pending request; rejecting.", streamId);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        try
        {
            await channel.Completion.Task.WaitAsync(context.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            channel.Completion.TrySetCanceled();
        }
    }
}
