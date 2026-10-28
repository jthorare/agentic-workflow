using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Connectors.Core.GitHub;

namespace Aw.Connectors.Testing.GitHub;

/// <summary>
/// A fake GitHub REST server for 'GET /repos/{o}/{r}/issues', '.../issues/comments', '.../issues/{n}/comments', '.../pulls/{n}' and '.../pulls/{n}/files':
/// an <see cref="HttpMessageHandler"/> that serves what was added to it the way GitHub does (since inclusive, oldest change first, per_page/page, a Link header with rel="next"),
/// and that answers scripted responses first (a rate limit, a 401, a redirect). It records every request.
/// </summary>
public sealed class FakeGitHubServer : HttpMessageHandler
{
    private readonly List<(string Repo, GitHubIssue Issue)> _issues = [];
    private readonly List<(string Repo, GitHubComment Comment)> _comments = [];
    private readonly Dictionary<(string Repo, int Number), GitHubPullDetail> _pulls = [];
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage?>> _script = new();
    private readonly List<RecordedRequest> _requests = [];

    /// <summary>The most entries the server serves a page, whatever per_page asks.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<RecordedRequest> Requests => _requests;

    /// <summary>Adds an entry to a repo's list.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="issue">The entry.</param>
    /// <returns>This server.</returns>
    public FakeGitHubServer Add(string repo, GitHubIssue issue)
    {
        _issues.Add((repo, issue));
        return this;
    }

    /// <summary>Adds a comment to a repo.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="comment">The comment.</param>
    /// <returns>This server.</returns>
    public FakeGitHubServer AddComment(string repo, GitHubComment comment)
    {
        _comments.Add((repo, comment));
        return this;
    }

    /// <summary>Sets what the single-pull-request routes say about a pull request; one without it reads as open, not a draft, no paths.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="detail">The detail.</param>
    /// <returns>This server.</returns>
    public FakeGitHubServer AddPull(string repo, int number, GitHubPullDetail detail)
    {
        _pulls[(repo, number)] = detail;
        return this;
    }

    /// <summary>Queues a scripted answer for the next request; returning null lets the request through to the normal behavior.</summary>
    /// <param name="respond">Builds the answer.</param>
    /// <returns>This server.</returns>
    public FakeGitHubServer Script(Func<HttpRequestMessage, HttpResponseMessage?> respond)
    {
        _script.Enqueue(respond);
        return this;
    }

    /// <summary>Queues a canned status for the next request.</summary>
    /// <param name="status">The status.</param>
    /// <returns>This server.</returns>
    public FakeGitHubServer ScriptStatus(HttpStatusCode status) => Script(_ => new HttpResponseMessage(status) { Content = new StringContent("{\"message\":\"scripted\"}") });

    /// <summary>Lets the next request through unchanged.</summary>
    /// <returns>This server.</returns>
    public FakeGitHubServer ScriptThrough() => Script(_ => null);

    /// <summary>Queues GitHub's primary rate-limit answer: 403 with no requests remaining and the reset time.</summary>
    /// <param name="reset">When the quota comes back.</param>
    /// <returns>This server.</returns>
    public FakeGitHubServer ScriptRateLimit(DateTimeOffset reset) => Script(_ =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{\"message\":\"API rate limit exceeded\"}") };
        response.Headers.Add("x-ratelimit-remaining", "0");
        response.Headers.Add("x-ratelimit-reset", reset.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        return response;
    });

    /// <summary>Queues GitHub's secondary rate-limit answer: the given status (403 or 429) with a retry-after.</summary>
    /// <param name="status">403 or 429.</param>
    /// <param name="retryAfterSeconds">The wait GitHub asks for.</param>
    /// <returns>This server.</returns>
    public FakeGitHubServer ScriptRetryAfter(HttpStatusCode status, int retryAfterSeconds) => Script(_ =>
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent("{\"message\":\"secondary rate limit\"}") };
        response.Headers.Add("retry-after", retryAfterSeconds.ToString(CultureInfo.InvariantCulture));
        return response;
    });

    /// <summary>Queues a redirect to <paramref name="location"/>.</summary>
    /// <param name="location">Where it points.</param>
    /// <returns>This server.</returns>
    public FakeGitHubServer ScriptRedirect(string location) => Script(_ =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
        response.Headers.Location = new Uri(location);
        return response;
    });

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), request.Headers.UserAgent.ToString()));
        if (_script.Count > 0 && _script.Dequeue()(request) is { } scripted)
        {
            return Task.FromResult(scripted);
        }

        return Task.FromResult(Serve(request.RequestUri!));
    }

    private HttpResponseMessage Serve(Uri uri)
    {
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rest = parts is ["api", "v3", ..] ? parts[2..] : parts;
        if (rest is not ["repos", var owner, var name, ..])
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var repo = $"{owner}/{name}";
        var query = ParseQuery(uri.Query);
        var since = query.TryGetValue("since", out var s) ? DateTimeOffset.Parse(s, CultureInfo.InvariantCulture) : DateTimeOffset.MinValue;
        return rest[3..] switch
        {
            ["issues"] => Paged(uri, query, _issues.Where(e => e.Repo == repo && e.Issue.UpdatedAt >= since)
                .Select(static e => e.Issue).OrderBy(static i => i.UpdatedAt).ThenBy(static i => i.Id).Select(ToJson)),
            ["issues", "comments"] => Paged(uri, query, _comments.Where(e => e.Repo == repo && e.Comment.UpdatedAt >= since)
                .Select(static e => e.Comment).OrderBy(static c => c.UpdatedAt).ThenBy(static c => c.Id).Select(ToJson)),
            ["issues", var n, "comments"] when int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                => Paged(uri, query, _comments.Where(e => e.Repo == repo && e.Comment.IssueNumber == number)
                    .Select(static e => e.Comment).OrderBy(static c => c.CreatedAt).ThenBy(static c => c.Id).Select(ToJson)),
            ["pulls", var n] when IsPull(repo, n, out var number) => Json(PullJson(repo, number)),
            ["pulls", var n, "files"] when IsPull(repo, n, out var number)
                => Paged(uri, query, Detail(repo, number).Paths.Select(static p => (JsonNode)new JsonObject { ["filename"] = p })),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private bool IsPull(string repo, string segment, out int number)
    {
        if (!int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            return false;
        }

        var wanted = number;
        return _pulls.ContainsKey((repo, wanted)) || _issues.Exists(e => e.Repo == repo && e.Issue.IsPullRequest && e.Issue.Number == wanted);
    }

    private GitHubPullDetail Detail(string repo, int number)
        => _pulls.TryGetValue((repo, number), out var detail) ? detail : new GitHubPullDetail(Merged: false, Draft: false, MergedAt: null, []);

    private JsonObject PullJson(string repo, int number)
    {
        var d = Detail(repo, number);
        return new JsonObject
        {
            ["number"] = number,
            ["merged"] = d.Merged,
            ["draft"] = d.Draft,
            ["merged_at"] = d.MergedAt?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        };
    }

    private static HttpResponseMessage Json(JsonNode body)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        response.Headers.Add("x-ratelimit-remaining", "4000");
        return response;
    }

    private HttpResponseMessage Paged(Uri uri, Dictionary<string, string> query, IEnumerable<JsonNode> sorted)
    {
        var perPage = Math.Min(MaxPageSize, Number(query, "per_page", 30));
        var page = Number(query, "page", 1);
        var matching = sorted.ToList();
        var array = new JsonArray();
        foreach (var entry in matching.Skip((page - 1) * perPage).Take(perPage))
        {
            array.Add(entry);
        }

        var response = Json(array);
        if (page * perPage < matching.Count)
        {
            var kept = string.Join('&', query.Where(kv => kv.Key != "page").Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
            var next = $"{uri.GetLeftPart(UriPartial.Path)}?{kept}&page={(page + 1).ToString(CultureInfo.InvariantCulture)}";
            response.Headers.Add("Link", $"<{next}>; rel=\"next\", <{next}>; rel=\"last\"");
        }

        return response;
    }

    private static JsonNode ToJson(GitHubComment c) => new JsonObject
    {
        ["id"] = c.Id,
        ["body"] = c.Body,
        ["issue_url"] = $"https://api.github.com/repos/acme/widgets/issues/{c.IssueNumber.ToString(CultureInfo.InvariantCulture)}",
        ["created_at"] = c.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        ["updated_at"] = c.UpdatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        ["user"] = c.Author is null ? null : new JsonObject { ["login"] = c.Author },
    };

    private static int Number(Dictionary<string, string> query, string key, int fallback)
        => query.TryGetValue(key, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;

    private static Dictionary<string, string> ParseQuery(string query)
        => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(static kv => kv.Split('=', 2))
            .ToDictionary(static kv => kv[0], static kv => Uri.UnescapeDataString(kv.Length > 1 ? kv[1] : string.Empty), StringComparer.Ordinal);

    private static JsonNode ToJson(GitHubIssue i)
    {
        var o = new JsonObject
        {
            ["id"] = i.Id,
            ["number"] = i.Number,
            ["title"] = i.Title,
            ["body"] = i.Body,
            ["state"] = i.State,
            ["html_url"] = i.Url,
            ["created_at"] = i.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["updated_at"] = i.UpdatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["closed_at"] = i.ClosedAt?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["user"] = i.Author is null ? null : new JsonObject { ["login"] = i.Author },
            ["comments"] = i.Comments,
            ["labels"] = new JsonArray([.. i.Labels.Select(static n => (JsonNode)new JsonObject { ["name"] = n })]),
        };
        if (i.IsPullRequest)
        {
            o["pull_request"] = new JsonObject { ["url"] = i.Url };
        }

        return o;
    }
}
