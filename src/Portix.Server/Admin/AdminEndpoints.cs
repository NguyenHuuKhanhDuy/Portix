using Microsoft.EntityFrameworkCore;
using Portix.Server.Auth;
using Portix.Server.Data;

namespace Portix.Server.Admin;

public sealed record CreateUserRequest(string Name, string? Plan);
public sealed record CreateUserResponse(Guid UserId, string Token);
public sealed record IssueTokenResponse(Guid TokenId, string Token);
public sealed record ChangePlanRequest(string Plan);

/// <summary>Operator-only endpoints for creating users, issuing/revoking their API tokens, and assigning plans. Gated by <see cref="AdminAuthFilter"/>.</summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin").AddEndpointFilter<AdminAuthFilter>();

        admin.MapPost("/users", async (CreateUserRequest request, PortixDbContext db, CancellationToken ct) =>
        {
            var planName = string.IsNullOrWhiteSpace(request.Plan) ? Plan.FreeName : request.Plan;
            var plan = await db.Plans.SingleOrDefaultAsync(p => p.Name == planName, ct).ConfigureAwait(false);
            if (plan is null)
            {
                return Results.BadRequest($"Unknown plan '{planName}'.");
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

            var plan = await db.Plans.SingleOrDefaultAsync(p => p.Name == request.Plan, ct).ConfigureAwait(false);
            if (plan is null)
            {
                return Results.BadRequest($"Unknown plan '{request.Plan}'.");
            }

            user.PlanId = plan.Id;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.NoContent();
        });
    }
}
