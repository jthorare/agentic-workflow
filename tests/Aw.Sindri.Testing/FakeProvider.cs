using System.Net;
using System.Text;

namespace Aw.Sindri.Testing;

/// <summary>
/// The GitHub, Linear, Slack and Google Calendar APIs in memory, as the innermost HttpMessageHandler of a poller's HttpClient. GitHub: any GET on
/// api.github.com answers the added events as the repo events array, newest first. Linear: any POST on api.linear.app answers
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

    private readonly Lock _gate = new();
    private readonly List<string> _githubEvents = [];
    private readonly List<string> _linearIssues = [];
    private readonly List<string> _slackMessages = [];
    private readonly List<string> _calendarEvents = [];
    private readonly List<ProviderRequest> _requests = [];
    private string? _slackError;
    private int _limitedLeft;
    private HttpStatusCode _limitStatus = HttpStatusCode.TooManyRequests;
    private TimeSpan? _limitRetryAfter;
    private Uri? _redirect;

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
    public void RateLimit(int times, TimeSpan? retryAfter, HttpStatusCode status = HttpStatusCode.TooManyRequests)
    {
        lock (_gate)
        {
            _limitedLeft = times;
            _limitRetryAfter = retryAfter;
            _limitStatus = status;
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
            _requests.Add(new ProviderRequest(request.RequestUri!, request.Method.Method, request.Headers.Authorization?.ToString(), body));   // why: HttpClient always sets the URI before the handler runs
            return Answer(request.RequestUri!);   // why: same
        }
    }

    private HttpResponseMessage Answer(Uri uri)
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
            var limited = new HttpResponseMessage(_limitStatus);
            if (_limitRetryAfter is { } wait)
            {
                limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(wait);
            }

            return limited;
        }

        var json = uri.Host switch
        {
            GitHubHost => "[" + string.Join(',', Enumerable.Reverse(_githubEvents)) + "]",
            LinearHost => "{\"data\":{\"issues\":{\"nodes\":[" + string.Join(',', _linearIssues) + "]}}}",
            SlackHost => _slackError is { } code
                ? "{\"ok\":false,\"error\":\"" + code + "\"}"
                : "{\"ok\":true,\"messages\":[" + string.Join(',', Enumerable.Reverse(_slackMessages)) + "]}",
            GoogleHost => "{\"items\":[" + string.Join(',', _calendarEvents) + "]}",
            _ => null,
        };
        return json is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
