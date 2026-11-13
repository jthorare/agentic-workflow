using System.Net;
using System.Text;

namespace Aw.Sindri.Testing;

/// <summary>
/// The GitHub, Linear, Slack and Google Calendar APIs in memory, as the innermost HttpMessageHandler of a poller's HttpClient. GitHub: a GET of
/// /repos/{owner}/{name}/pulls/{n} answers from <see cref="GitHubPulls"/>; any other GET on api.github.com answers the added events as the
/// repo events array, newest first. Linear: any POST on api.linear.app answers
/// the added issues as {"data":{"issues":{"nodes":[...]}}}. A queued rate limit answers the next requests with 429 (or another
/// status) and an optional Retry-After in seconds; a redirect answers every request with a 302 to another URL. Each request is
/// recorded with the Authorization header it carried. Test infrastructure that works: the red tests need it to run.
/// </summary>
public sealed class FakeProvider : HttpMessageHandler
{
    /// <summary>The GitHub API host.</summary>
    public const string GitHubHost = "api.github.com";

    /// <summary>The Linear API host.</summary>
    public const string LinearHost = "api.linear.app";

    /// <summary>The Slack API host.</summary>
    public const string SlackHost = "slack.com";

    /// <summary>The Google API host.</summary>
    public const string GoogleHost = "www.googleapis.com";

    private const int GitHubPageSize = 100;
    private const int LinearPageSize = 50;
    private const int SlackPageSize = 100;

    private readonly Lock _gate = new();
    private readonly List<string> _githubEvents = [];
    private readonly List<string> _linearIssues = [];
    private readonly List<string> _slackMessages = [];
    private readonly List<string> _calendarEvents = [];
    private readonly List<ProviderRequest> _requests = [];
    private string? _slackError;
    private bool _unreachable;
    private bool _historyOldestFirst;
    private int _limitedLeft;
    private IReadOnlyDictionary<string, string>? _limitHeaders;
    private string? _limitBody;
    private HttpStatusCode _limitStatus = HttpStatusCode.TooManyRequests;
    private TimeSpan? _limitRetryAfter;
    private Uri? _redirect;

    /// <summary>Gets GitHub's single pull request answers.</summary>
    public FakeGitHubPulls GitHubPulls { get; } = new();

    /// <summary>Gets the requests received, in order.</summary>
    public IReadOnlyList<ProviderRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Adds a GitHub event (a JSON object from <see cref="ProviderFixtures"/>). The fake answers newest first, by the order added reversed.</summary>
    /// <param name="json">The event.</param>
    public void AddGitHubEvent(string json)
    {
        lock (_gate)
        {
            _githubEvents.Add(json);
        }
    }

    /// <summary>Adds a Linear issue with its history (a JSON object from <see cref="ProviderFixtures"/>).</summary>
    /// <param name="json">The issue.</param>
    public void AddLinearIssue(string json)
    {
        lock (_gate)
        {
            _linearIssues.Add(json);
        }
    }

    /// <summary>Adds a Slack message (from <see cref="ProviderFixtures"/>); conversations.history answers newest first.</summary>
    /// <param name="json">The message.</param>
    public void AddSlackMessage(string json)
    {
        lock (_gate)
        {
            _slackMessages.Add(json);
        }
    }

    /// <summary>Makes every Slack request answer HTTP 200 with ok=false and this error code.</summary>
    /// <param name="code">Slack's error code ('ratelimited', 'invalid_auth').</param>
    public void SlackError(string code)
    {
        lock (_gate)
        {
            _slackError = code;
        }
    }

    /// <summary>Adds a calendar event (from <see cref="ProviderFixtures"/>).</summary>
    /// <param name="json">The event.</param>
    public void AddCalendarEvent(string json)
    {
        lock (_gate)
        {
            _calendarEvents.Add(json);
        }
    }

    /// <summary>Removes every calendar event (to put them back moved).</summary>
    public void ClearCalendarEvents()
    {
        lock (_gate)
        {
            _calendarEvents.Clear();
        }
    }

    /// <summary>Makes the next requests fail with a rate-limit status.</summary>
    /// <param name="times">How many requests fail.</param>
    /// <param name="retryAfter">The Retry-After header, or null for none.</param>
    /// <param name="status">The status (429 by default; 503 for a 5xx).</param>
    /// <param name="headers">Extra response headers ('x-ratelimit-remaining'), or null.</param>
    /// <param name="body">The response body, or null for none.</param>
    public void RateLimit(int times, TimeSpan? retryAfter, HttpStatusCode status = HttpStatusCode.TooManyRequests, IReadOnlyDictionary<string, string>? headers = null, string? body = null)
    {
        lock (_gate)
        {
            _limitHeaders = headers;
            _limitBody = body;
            _limitedLeft = times;
            _limitRetryAfter = retryAfter;
            _limitStatus = status;
        }
    }

    /// <summary>Makes Linear serve an issue's history oldest first (it serves newest first by default).</summary>
    public void LinearHistoryOldestFirst()
    {
        lock (_gate)
        {
            _historyOldestFirst = true;
        }
    }

    /// <summary>Makes every request fail as a network error (HttpRequestException).</summary>
    public void Unreachable()
    {
        lock (_gate)
        {
            _unreachable = true;
        }
    }

    /// <summary>Makes every request answer 302 to another URL.</summary>
    /// <param name="target">Where to send the caller.</param>
    public void RedirectTo(Uri target)
    {
        lock (_gate)
        {
            _redirect = target;
        }
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            if (_unreachable)
            {
                throw new HttpRequestException("the fake provider is unreachable");
            }

            _requests.Add(new ProviderRequest(request.RequestUri!, request.Method.Method, request.Headers.Authorization?.ToString(), body));   // why: HttpClient always sets the URI before the handler runs
            return Answer(request.RequestUri!, body);   // why: same
        }
    }

    private HttpResponseMessage Answer(Uri uri, string requestBody)
    {
        if (_redirect is not null)
        {
            var moved = new HttpResponseMessage(HttpStatusCode.Redirect);
            moved.Headers.Location = _redirect;
            return moved;
        }

        if (_limitedLeft > 0)
        {
            _limitedLeft--;
            return Limited();
        }

        if (uri.Host == GitHubHost && GitHubPulls.Answer(uri) is { } pull)
        {
            return pull;
        }

        string? link = null;
        var json = uri.Host switch
        {
            GitHubHost => GitHubPage(uri, out link),
            LinearHost => LinearPage(requestBody),
            SlackHost => SlackPage(uri),
            GoogleHost => "{\"items\":[" + string.Join(',', _calendarEvents) + "]}",
            _ => null,
        };
        if (json is null)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var ok = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (link is not null)
        {
            _ = ok.Headers.TryAddWithoutValidation("Link", link);
        }

        return ok;
    }

    private HttpResponseMessage Limited()
    {
        var limited = new HttpResponseMessage(_limitStatus);
        if (_limitRetryAfter is { } wait)
        {
            limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(wait);
        }

        foreach (var (name, value) in _limitHeaders ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            _ = limited.Headers.TryAddWithoutValidation(name, value);
        }

        if (_limitBody is { } text)
        {
            limited.Content = new StringContent(text, Encoding.UTF8, "application/json");
        }

        return limited;
    }

    // GitHub: newest first, 100 to a page, a Link header while more remain.
    private string GitHubPage(Uri uri, out string? link)
    {
        var page = int.TryParse(System.Web.HttpUtility.ParseQueryString(uri.Query)["page"], out var asked) && asked > 0 ? asked : 1;
        var all = Enumerable.Reverse(_githubEvents).ToList();
        link = page * GitHubPageSize < all.Count ? $"<https://{GitHubHost}{uri.AbsolutePath}?per_page={GitHubPageSize}&page={(page + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}>; rel=\"next\"" : null;
        return "[" + string.Join(',', all.Skip((page - 1) * GitHubPageSize).Take(GitHubPageSize)) + "]";
    }

    // Slack: newest first, 100 to a page, response_metadata.next_cursor is the next offset while more remain.
    private string SlackPage(Uri uri)
    {
        if (_slackError is { } code)
        {
            return "{\"ok\":false,\"error\":\"" + code + "\"}";
        }

        var offset = int.TryParse(System.Web.HttpUtility.ParseQueryString(uri.Query)["cursor"], out var asked) && asked > 0 ? asked : 0;
        var all = Enumerable.Reverse(_slackMessages).ToList();
        var end = Math.Min(offset + SlackPageSize, all.Count);
        var next = end < all.Count ? end.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        return "{\"ok\":true,\"messages\":[" + string.Join(',', all.Skip(offset).Take(SlackPageSize)) + "],\"response_metadata\":{\"next_cursor\":\"" + next + "\"}}";
    }

    // Linear: issues in the order added, 50 to a page, variables.after is the offset the last page's endCursor named.
    private string LinearPage(string requestBody)
    {
        var offset = 0;
        DateTimeOffset? since = null;
        if (requestBody.Length > 0)
        {
            using var document = System.Text.Json.JsonDocument.Parse(requestBody);
            if (document.RootElement.TryGetProperty("query", out var text) && LinearWindow.FiltersHistory(text.GetString()))
            {
                return LinearWindow.UnknownHistoryFilter;
            }

            if (document.RootElement.TryGetProperty("variables", out var variables))
            {
                offset = variables.TryGetProperty("after", out var after) && int.TryParse(after.GetString(), out var parsed) ? parsed : 0;
                since = variables.TryGetProperty("since", out var floor) && DateTimeOffset.TryParse(floor.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var when) ? when : null;
            }
        }

        var end = Math.Min(offset + LinearPageSize, _linearIssues.Count);
        var more = end < _linearIssues.Count ? "true" : "false";
        var page = _linearIssues.Skip(offset).Take(LinearPageSize).Select(issue => LinearWindow.Apply(issue, since, _historyOldestFirst));
        return "{\"data\":{\"issues\":{\"nodes\":[" + string.Join(',', page) + "],\"pageInfo\":{\"hasNextPage\":" + more + ",\"endCursor\":\"" + end.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"}}}}";
    }
}
