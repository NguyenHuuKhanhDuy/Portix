namespace Portix.Server.Forwarding;

/// <summary>
/// The server's two ends of one client-opened /data/{streamId} connection:
/// the client's request body (server reads the local app's response from it)
/// and the client's response body (server writes the raw public request into it).
/// Each direction of an HTTP/2 stream can half-close independently, so
/// <see cref="CompleteWriteToClientAsync"/> ends the write direction without
/// affecting the still-open read direction.
/// </summary>
public sealed class DataChannel
{
    public required Stream ReadFromClient { get; init; }
    public required Stream WriteToClient { get; init; }
    public required Func<Task> CompleteWriteToClientAsync { get; init; }

    /// <summary>Set once both directions of the exchange have finished, so the /data/{streamId} request delegate can return.</summary>
    public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
