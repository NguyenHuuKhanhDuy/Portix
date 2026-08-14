using Microsoft.EntityFrameworkCore;
using Portix.Server.Auth;
using Portix.Server.Data;
using Portix.Server.Sessions;

namespace Portix.Server.Admin;

public sealed record CreateUserRequest(string Name, int? PlanId);
public sealed record CreateUserResponse(Guid UserId, string Token);
public sealed record IssueTokenResponse(Guid TokenId, string Token);
public sealed record RestoreTokenRequest(string Token);
public sealed record ChangePlanRequest(int PlanId);
public sealed record SetUserStatusRequest(bool IsDisabled);
public sealed record CreatePlanRequest(string Name, int MaxConcurrentTunnels);
public sealed record UpdatePlanRequest(string Name, int MaxConcurrentTunnels);
public sealed record AdminUserSummaryDto(Guid Id, string Name, string PlanName, bool IsDisabled, DateTimeOffset CreatedAtUtc, int ActiveTunnelCount);
public sealed record AdminTokenSummaryDto(Guid Id, DateTimeOffset CreatedAtUtc, bool IsActive);
public sealed record AdminPlanSummaryDto(int Id, string Name, int MaxConcurrentTunnels);
public sealed record AdminTunnelSummaryDto(string TunnelId, string Subdomain, int LocalPortHint);
public sealed record AdminSessionSummaryDto(string SessionId, Guid OwnerId, string OwnerName, DateTimeOffset LastHeartbeatUtc, IReadOnlyList<AdminTunnelSummaryDto> Tunnels);

/// <summary>Operator-only endpoints for creating users, issuing/revoking their API tokens, and assigning plans. Gated by <see cref="AdminAuthFilter"/>.</summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin").AddEndpointFilter<AdminAuthFilter>();

        admin.MapPost("/users", async (CreateUserRequest request, PortixDbContext db, CancellationToken ct) =>
        {
            var planId = request.PlanId ?? Plan.FreeId;
            var plan = await db.Plans.FindAsync([planId], ct).ConfigureAwait(false);
            if (plan is null)
            {
                return Results.BadRequest($"Unknown plan id {planId}.");
            }

            var user = new User { Id = Guid.NewGuid(), Name = request.Name, PlanId = plan.Id };
            db.Users.Add(user);

            var rawToken = TokenHasher.GenerateRawToken();
            db.ApiTokens.Add(new ApiToken { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = TokenHasher.Hash(rawToken) });

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.Ok(new CreateUserResponse(user.Id, rawToken));
        });

        admin.MapPost("/users/{id:guid}/tokens", async (Guid id, PortixDbContext db, CancellationToken ct) =>
        {
            var userExists = await db.Users.AnyAsync(u => u.Id == id, ct).ConfigureAwait(false);
            if (!userExists)
            {
                return Results.NotFound();
            }

            var rawToken = TokenHasher.GenerateRawToken();
            var token = new ApiToken { Id = Guid.NewGuid(), UserId = id, TokenHash = TokenHasher.Hash(rawToken) };
            db.ApiTokens.Add(token);

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.Ok(new IssueTokenResponse(token.Id, rawToken));
        });

        admin.MapPost("/users/{id:guid}/tokens/restore", async (Guid id, RestoreTokenRequest request, PortixDbContext db, CancellationToken ct) =>
        {
            var userExists = await db.Users.AnyAsync(u => u.Id == id, ct).ConfigureAwait(false);
            if (!userExists)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return Results.BadRequest("Token must not be empty.");
            }

            var tokenHash = TokenHasher.Hash(request.Token);
            if (await db.ApiTokens.AnyAsync(t => t.TokenHash == tokenHash, ct).ConfigureAwait(false))
            {
                return Results.Conflict("This token is already registered.");
            }

            var token = new ApiToken { Id = Guid.NewGuid(), UserId = id, TokenHash = tokenHash };
            db.ApiTokens.Add(token);

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.Ok(new IssueTokenResponse(token.Id, request.Token));
        });

        admin.MapDelete("/tokens/{id:guid}", async (Guid id, PortixDbContext db, CancellationToken ct) =>
        {
            var token = await db.ApiTokens.FindAsync([id], ct).ConfigureAwait(false);
            if (token is null)
            {
                return Results.NotFound();
            }

            token.RevokedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.NoContent();
        });

        admin.MapPut("/users/{id:guid}/plan", async (Guid id, ChangePlanRequest request, PortixDbContext db, CancellationToken ct) =>
        {
            var user = await db.Users.FindAsync([id], ct).ConfigureAwait(false);
            if (user is null)
            {
                return Results.NotFound();
            }

            var plan = await db.Plans.FindAsync([request.PlanId], ct).ConfigureAwait(false);
            if (plan is null)
            {
                return Results.BadRequest($"Unknown plan id {request.PlanId}.");
            }

            user.PlanId = plan.Id;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.NoContent();
        });

        admin.MapGet("/users", async (PortixDbContext db, SessionRegistry registry, CancellationToken ct) =>
        {
            var users = await db.Users.Include(u => u.Plan).ToListAsync(ct).ConfigureAwait(false);
            return Results.Ok(users.Select(u => ToSummary(u, registry)));
        });

        admin.MapGet("/users/{id:guid}", async (Guid id, PortixDbContext db, SessionRegistry registry, CancellationToken ct) =>
        {
            var user = await db.Users.Include(u => u.Plan).SingleOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false);
            return user is null ? Results.NotFound() : Results.Ok(ToSummary(user, registry));
        });

        admin.MapPut("/users/{id:guid}/status", async (Guid id, SetUserStatusRequest request, PortixDbContext db, CancellationToken ct) =>
        {
            var user = await db.Users.FindAsync([id], ct).ConfigureAwait(false);
            if (user is null)
            {
                return Results.NotFound();
            }

            user.IsDisabled = request.IsDisabled;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.NoContent();
        });

        admin.MapDelete("/users/{id:guid}", async (Guid id, PortixDbContext db, SessionRegistry registry, CancellationToken ct) =>
        {
            var user = await db.Users.FindAsync([id], ct).ConfigureAwait(false);
            if (user is null)
            {
                return Results.NotFound();
            }

            // Torn down before the row disappears so an already-connected session (issued on a
            // now-to-be-deleted user's token) can't keep tunnels open past the delete.
            foreach (var session in registry.ActiveSessions.Where(s => s.Owner.Id == id))
            {
                registry.Teardown(session, new InvalidOperationException("User deleted by admin."));
            }

            db.Users.Remove(user);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.NoContent();
        });

        admin.MapGet("/users/{id:guid}/tokens", async (Guid id, PortixDbContext db, CancellationToken ct) =>
        {
            var userExists = await db.Users.AnyAsync(u => u.Id == id, ct).ConfigureAwait(false);
            if (!userExists)
            {
                return Results.NotFound();
            }

            var tokens = await db.ApiTokens
                .Where(t => t.UserId == id)
                .Select(t => new AdminTokenSummaryDto(t.Id, t.CreatedAtUtc, t.RevokedAtUtc == null))
                .ToListAsync(ct).ConfigureAwait(false);

            return Results.Ok(tokens);
        });

        admin.MapGet("/plans", async (PortixDbContext db, CancellationToken ct) =>
        {
            var plans = await db.Plans
                .Select(p => new AdminPlanSummaryDto(p.Id, p.Name, p.MaxConcurrentTunnels))
                .ToListAsync(ct).ConfigureAwait(false);

            return Results.Ok(plans);
        });

        admin.MapPost("/plans", async (CreatePlanRequest request, PortixDbContext db, CancellationToken ct) =>
        {
            if (await db.Plans.AnyAsync(p => p.Name == request.Name, ct).ConfigureAwait(false))
            {
                return Results.Conflict($"A plan named '{request.Name}' already exists.");
            }

            var plan = new Plan { Name = request.Name, MaxConcurrentTunnels = request.MaxConcurrentTunnels };
            db.Plans.Add(plan);

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.Ok(new AdminPlanSummaryDto(plan.Id, plan.Name, plan.MaxConcurrentTunnels));
        });

        admin.MapPut("/plans/{id:int}", async (int id, UpdatePlanRequest request, PortixDbContext db, CancellationToken ct) =>
        {
            var plan = await db.Plans.FindAsync([id], ct).ConfigureAwait(false);
            if (plan is null)
            {
                return Results.NotFound();
            }

            if (await db.Plans.AnyAsync(p => p.Id != id && p.Name == request.Name, ct).ConfigureAwait(false))
            {
                return Results.Conflict($"A plan named '{request.Name}' already exists.");
            }

            plan.Name = request.Name;
            plan.MaxConcurrentTunnels = request.MaxConcurrentTunnels;

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.NoContent();
        });

        admin.MapDelete("/plans/{id:int}", async (int id, PortixDbContext db, CancellationToken ct) =>
        {
            var plan = await db.Plans.FindAsync([id], ct).ConfigureAwait(false);
            if (plan is null)
            {
                return Results.NotFound();
            }

            // The User -> Plan foreign key is DeleteBehavior.Restrict, so the database would reject
            // this anyway — checked here first for a clear 409 instead of an unhandled DbUpdateException.
            if (await db.Users.AnyAsync(u => u.PlanId == id, ct).ConfigureAwait(false))
            {
                return Results.Conflict("Cannot delete a plan that still has users assigned to it.");
            }

            db.Plans.Remove(plan);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.NoContent();
        });

        admin.MapGet("/sessions", (SessionRegistry registry) =>
            Results.Ok(registry.ActiveSessions.Select(ToSummary)));

        admin.MapDelete("/sessions/{id}", (string id, SessionRegistry registry) =>
        {
            if (!registry.TryGetSession(id, out var session))
            {
                return Results.NotFound();
            }

            registry.Teardown(session, new InvalidOperationException("Disconnected by admin."));
            return Results.NoContent();
        });
    }

    private static AdminUserSummaryDto ToSummary(User user, SessionRegistry registry) =>
        new(user.Id, user.Name, user.Plan?.Name ?? "?", user.IsDisabled, user.CreatedAtUtc, registry.CountActiveTunnels(user.Id));

    private static AdminSessionSummaryDto ToSummary(Session session) => new(
        session.SessionId,
        session.Owner.Id,
        session.Owner.Name,
        session.LastHeartbeatUtc,
        session.Tunnels.Values.Select(t => new AdminTunnelSummaryDto(t.TunnelId, t.Subdomain, t.LocalPortHint)).ToList());
}
