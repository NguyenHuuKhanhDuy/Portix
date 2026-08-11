namespace Portix.Server.Data;

/// <summary>A subscription tier: how many tunnels a user on this plan may have registered at once.</summary>
public sealed class Plan
{
    public const string FreeName = "Free";
    public const string ProName = "Pro";

    public int Id { get; set; }
    public required string Name { get; set; }
    public required int MaxConcurrentTunnels { get; set; }
}
