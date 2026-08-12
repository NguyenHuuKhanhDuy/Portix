namespace Portix.Shared.Protocol;

/// <summary>
/// Detects an HTTP protocol-upgrade request (e.g. a WebSocket handshake) from its `Connection`
/// and `Upgrade` header values, per RFC 7230 §6.7: `Connection` is a comma-separated list of
/// tokens, one of which must be the literal token "Upgrade" (case-insensitive) alongside a
/// present `Upgrade` header.
/// </summary>
public static class UpgradeRequestDetector
{
    public static bool IsUpgrade(IEnumerable<string>? connectionValues, IEnumerable<string>? upgradeValues)
    {
        if (upgradeValues is null || !upgradeValues.Any())
        {
            return false;
        }

        if (connectionValues is null)
        {
            return false;
        }

        foreach (var value in connectionValues)
        {
            foreach (var token in value.Split(','))
            {
                if (token.Trim().Equals("Upgrade", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
