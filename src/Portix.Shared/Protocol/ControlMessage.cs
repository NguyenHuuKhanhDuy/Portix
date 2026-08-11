namespace Portix.Shared.Protocol;

public static class ControlMessageType
{
    public const string Hello = "hello";
    public const string HelloAck = "hello_ack";
    public const string HelloReject = "hello_reject";
    public const string Ping = "ping";
    public const string Pong = "pong";
    public const string NewStream = "new_stream";
    public const string RegisterTunnel = "register_tunnel";
    public const string TunnelRegistered = "tunnel_registered";
    public const string TunnelRegisterRejected = "tunnel_register_rejected";
    public const string UnregisterTunnel = "unregister_tunnel";
    public const string TunnelUnregistered = "tunnel_unregistered";
}

/// <summary>
/// Envelope for every message sent over the control channel. Only the fields
/// relevant to <see cref="Type"/> are populated; the rest are null.
/// </summary>
public sealed class ControlMessage
{
    public required string Type { get; set; }

    /// <summary>Hello: shared-secret auth token.</summary>
    public string? Token { get; set; }

    /// <summary>HelloAck: server-assigned session id.</summary>
    public string? SessionId { get; set; }

    /// <summary>HelloAck: port the public listener is reachable on, so the client can display a full URL.</summary>
    public int? PublicPort { get; set; }

    /// <summary>HelloAck: hostname suffix the subdomain is nested under (e.g. "localtest.me").</summary>
    public string? PublicHostSuffix { get; set; }

    /// <summary>HelloReject / TunnelRegisterRejected: human-readable rejection reason.</summary>
    public string? Reason { get; set; }

    /// <summary>NewStream: id correlating a public request to a /data/{id} connection.</summary>
    public string? StreamId { get; set; }

    /// <summary>NewStream: which tunnel this stream's traffic belongs to.</summary>
    public string? TunnelId { get; set; }

    /// <summary>RegisterTunnel/TunnelRegistered/TunnelRegisterRejected/UnregisterTunnel/TunnelUnregistered: correlates a client-initiated request with its reply.</summary>
    public string? RequestId { get; set; }

    /// <summary>RegisterTunnel: local port the client wants exposed.</summary>
    public int? LocalPort { get; set; }

    /// <summary>RegisterTunnel: subdomain the client would like, if any; server assigns a random one otherwise.</summary>
    public string? DesiredSubdomain { get; set; }

    /// <summary>TunnelRegistered: subdomain assigned to the new tunnel.</summary>
    public string? Subdomain { get; set; }

    /// <summary>TunnelRegistered: full public URL for the new tunnel.</summary>
    public string? PublicUrl { get; set; }

    public static ControlMessage CreateHello(string token) =>
        new() { Type = ControlMessageType.Hello, Token = token };

    public static ControlMessage CreateHelloAck(string sessionId, int publicPort, string publicHostSuffix) =>
        new()
        {
            Type = ControlMessageType.HelloAck,
            SessionId = sessionId,
            PublicPort = publicPort,
            PublicHostSuffix = publicHostSuffix,
        };

    public static ControlMessage CreateHelloReject(string reason) =>
        new() { Type = ControlMessageType.HelloReject, Reason = reason };

    public static ControlMessage CreatePing() => new() { Type = ControlMessageType.Ping };

    public static ControlMessage CreatePong() => new() { Type = ControlMessageType.Pong };

    public static ControlMessage CreateNewStream(string streamId, string tunnelId) =>
        new() { Type = ControlMessageType.NewStream, StreamId = streamId, TunnelId = tunnelId };

    public static ControlMessage CreateRegisterTunnel(string requestId, int localPort, string? desiredSubdomain) =>
        new()
        {
            Type = ControlMessageType.RegisterTunnel,
            RequestId = requestId,
            LocalPort = localPort,
            DesiredSubdomain = desiredSubdomain,
        };

    public static ControlMessage CreateTunnelRegistered(string requestId, string tunnelId, string subdomain, string publicUrl) =>
        new()
        {
            Type = ControlMessageType.TunnelRegistered,
            RequestId = requestId,
            TunnelId = tunnelId,
            Subdomain = subdomain,
            PublicUrl = publicUrl,
        };

    public static ControlMessage CreateTunnelRegisterRejected(string requestId, string reason) =>
        new() { Type = ControlMessageType.TunnelRegisterRejected, RequestId = requestId, Reason = reason };

    public static ControlMessage CreateUnregisterTunnel(string requestId, string tunnelId) =>
        new() { Type = ControlMessageType.UnregisterTunnel, RequestId = requestId, TunnelId = tunnelId };

    public static ControlMessage CreateTunnelUnregistered(string requestId, string tunnelId) =>
        new() { Type = ControlMessageType.TunnelUnregistered, RequestId = requestId, TunnelId = tunnelId };
}
