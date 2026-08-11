using Microsoft.EntityFrameworkCore;
using Portix.Server.Data;

namespace Portix.Server.Auth;

/// <summary>Resolves a presented API token to its owning, enabled user.</summary>
public sealed class AuthService(PortixDbContext db)
{
    public async Task<User?> AuthenticateAsync(string? presentedToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(presentedToken))
        {
            return null;
        }

        var hash = TokenHasher.Hash(presentedToken);

        var token = await db.ApiTokens
            .Include(t => t.User!.Plan)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct)
            .ConfigureAwait(false);

        if (token is null || !token.IsActive || token.User is null || token.User.IsDisabled)
        {
            return null;
        }

        return token.User;
    }
}
