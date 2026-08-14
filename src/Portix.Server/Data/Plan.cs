namespace Portix.Server.Data;

/// <summary>A subscription tier: how many tunnels a user on this plan may have registered at once.</summary>
public sealed class Plan
{
    public const string FreeName = "Free";
    public const string ProName = "Pro";

    // Matches the seeded rows in PortixDbContext.OnModelCreating — used as the default plan id
    // when creating a user without specifying one.
    public const int FreeId = 1;

    public int Id { get; set; }
    public required string Name { get; set; }
    public required int MaxConcurrentTunnels { get; set; }
}
