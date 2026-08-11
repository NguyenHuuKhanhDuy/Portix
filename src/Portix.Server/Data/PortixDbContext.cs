using Microsoft.EntityFrameworkCore;

namespace Portix.Server.Data;

public sealed class PortixDbContext(DbContextOptions<PortixDbContext> options) : DbContext(options)
{
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Plan>(plan =>
        {
            plan.HasIndex(p => p.Name).IsUnique();
            plan.HasData(
                new Plan { Id = 1, Name = Plan.FreeName, MaxConcurrentTunnels = 1 },
                new Plan { Id = 2, Name = Plan.ProName, MaxConcurrentTunnels = 5 });
        });

        modelBuilder.Entity<User>(user =>
        {
            user.HasOne(u => u.Plan)
                .WithMany()
                .HasForeignKey(u => u.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiToken>(token =>
        {
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
