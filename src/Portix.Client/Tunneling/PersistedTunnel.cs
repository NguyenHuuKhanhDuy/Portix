namespace Portix.Client.Tunneling;

/// <summary>A tunnel as recorded in the on-disk session mirror (see TunnelSessionStore) — enough to reopen it.</summary>
public sealed record PersistedTunnel(string Scheme, int LocalPort, string? Subdomain);
