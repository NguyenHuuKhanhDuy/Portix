namespace Portix.Server.Data;

/// <summary>A hashed, revocable credential presented on the control-channel handshake. The raw token is never stored.</summary>
public sealed class ApiToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAtUtc { get; set; }

    public bool IsActive => RevokedAtUtc is null;
}
