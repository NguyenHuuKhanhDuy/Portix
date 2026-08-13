using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace Portix.Client.Cli.Update;

public static class GitHubReleaseClient
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/NguyenHuuKhanhDuy/Portix/releases/latest";

    /// <summary>Returns null if the repo has no releases published yet — a normal, expected outcome, not an error.</summary>
    public static async Task<GitHubRelease?> GetLatestReleaseAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        // The GitHub REST API rejects requests with no User-Agent header.
        request.Headers.UserAgent.ParseAdd("Portix-Client");

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
