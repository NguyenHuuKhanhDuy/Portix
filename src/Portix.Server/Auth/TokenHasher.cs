using System.Security.Cryptography;
using System.Text;

namespace Portix.Server.Auth;

/// <summary>Hashes API tokens for storage/lookup so raw token values are never persisted.</summary>
public static class TokenHasher
{
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }

    /// <summary>Generates a new opaque, cryptographically random raw token (32 bytes, base64url-encoded).</summary>
    public static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
