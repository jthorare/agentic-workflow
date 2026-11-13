using System.Globalization;
using System.Net;
using System.Text;

namespace Aw.Sindri.Testing;

/// <summary>
/// GitHub's GET /repos/{owner}/{name}/pulls/{n} in memory, answered through <see cref="FakeProvider.GitHubPulls"/>: a pull added here
/// answers with its status and body, and any other number answers 404.
/// </summary>
public sealed class FakeGitHubPulls
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, (HttpStatusCode Status, string Body)> _pulls = [];

    /// <summary>Sets the answer for one pull request.</summary>
    /// <param name="number">The pull request number.</param>
    /// <param name="body">The response body; <see cref="ProviderFixtures.GitHubPull"/> makes a good one.</param>
    /// <param name="status">The status, 200 by default.</param>
    public void Add(int number, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        lock (_gate)
        {
            _pulls[number] = (status, body);
        }
    }

    /// <summary>Answers a GET of one pull request.</summary>
    /// <param name="uri">The request URI.</param>
    /// <returns>The answer, or null when the URI is not /repos/{owner}/{name}/pulls/{n}.</returns>
    internal HttpResponseMessage? Answer(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is not ["repos", _, _, "pulls", var last] || !int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        lock (_gate)
        {
            return _pulls.TryGetValue(number, out var pull)
                ? new HttpResponseMessage(pull.Status) { Content = new StringContent(pull.Body, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
