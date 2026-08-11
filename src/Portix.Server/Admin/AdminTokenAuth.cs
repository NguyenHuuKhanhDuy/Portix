using System.Security.Cryptography;
using System.Text;

namespace Portix.Server.Admin;

/// <summary>Validates the separate operator bearer token that gates /admin/* endpoints (distinct from per-user API tokens).</summary>
public sealed class AdminTokenAuth(string adminToken)
{
    private readonly byte[] _expectedTokenBytes = Encoding.UTF8.GetBytes(adminToken);

    public bool IsValid(string? presentedToken)
    {
        if (string.IsNullOrEmpty(presentedToken))
        {
            return false;
        }

        var presentedBytes = Encoding.UTF8.GetBytes(presentedToken);

        if (presentedBytes.Length != _expectedTokenBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(presentedBytes, _expectedTokenBytes);
    }
}
