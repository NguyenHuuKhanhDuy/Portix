namespace Portix.Client.Cli.Update;

public static class ReleaseVersion
{
    /// <summary>
    /// Parses a version string that may have a leading 'v' (git tag style, e.g. "v1.2.0") and/or
    /// a '+build metadata' suffix (SourceRevisionId, e.g. "1.2.0+abcd123"), returning null if
    /// what's left isn't a parseable System.Version.
    /// </summary>
    public static Version? Parse(string raw)
    {
        var value = raw.StartsWith('v') ? raw[1..] : raw;

        var plusIndex = value.IndexOf('+');
        if (plusIndex >= 0)
        {
            value = value[..plusIndex];
        }

        return Version.TryParse(value, out var version) ? version : null;
    }
}
