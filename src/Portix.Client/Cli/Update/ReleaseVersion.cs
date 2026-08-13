namespace Portix.Client.Cli.Update;

public static class ReleaseVersion
{
    /// <summary>
    /// Parses a version string that may have a leading 'v' (git tag style, e.g. "v1.2.0"), a
    /// semver pre-release suffix (e.g. "1.2.0-rc1", "0.0.1-test"), and/or a '+build metadata'
    /// suffix (SourceRevisionId, e.g. "1.2.0+abcd123") — stripping either/both before parsing the
    /// numeric core with System.Version. Returns null if what's left isn't parseable.
    /// </summary>
    public static Version? Parse(string raw)
    {
        var value = raw.StartsWith('v') ? raw[1..] : raw;

        var suffixIndex = value.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            value = value[..suffixIndex];
        }

        return Version.TryParse(value, out var version) ? version : null;
    }
}
