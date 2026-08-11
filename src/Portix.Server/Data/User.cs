namespace Portix.Server.Data;

/// <summary>A Portix account. Owns zero or more <see cref="ApiToken"/>s and is assigned exactly one <see cref="Plan"/>.</summary>
public sealed class User
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDisabled { get; set; }
}
