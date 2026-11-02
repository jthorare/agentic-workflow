using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Testing.GitHub;

namespace Aw.Connectors.Testing.Slack;

/// <summary>
/// A fake Slack Web API for 'conversations.history' and 'conversations.replies': an <see cref="HttpMessageHandler"/> that serves what was added the way Slack does (HTTP 200 with
/// {"ok":true,…} or {"ok":false,"error":…}, newest first, a response_metadata.next_cursor to page on) and answers scripted responses first (a 429 with Retry-After, ok:false, a redirect, a
/// reply that reflects the token). It records every request and FAILS the run (throws) on a request for a channel that is not allowed or for any route it does not know.
/// </summary>
public sealed class FakeSlackServer(params string[] allowedChannels) : HttpMessageHandler
{
    private readonly SlackStore _store = new();
    private readonly HashSet<string> _allowed = new(allowedChannels ?? [], StringComparer.Ordinal);
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage?>> _script = new();
    private readonly List<RecordedRequest> _requests = [];

    /// <summary>The most messages the server serves a page, whatever limit asks.</summary>
    public int MaxPageSize { get; set; } = 200;

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<RecordedRequest> Requests => _requests;

    /// <summary>Adds a message to a channel (a reply carries its root's ts as thread_ts).</summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="message">The message.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer Add(string channelId, SlackMessage message)
    {
        _store.Add(channelId, message);
        return this;
    }

    /// <summary>Queues a scripted answer for the next request; returning null lets the request through to the normal behavior.</summary>
    /// <param name="respond">Builds the answer.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer Script(Func<HttpRequestMessage, HttpResponseMessage?> respond)
    {
        _script.Enqueue(respond);
        return this;
    }

    /// <summary>Lets the next request through unchanged.</summary>
    /// <returns>This server.</returns>
    public FakeSlackServer ScriptThrough() => Script(_ => null);

    /// <summary>Queues Slack's rate-limit answer: HTTP 429 with a Retry-After.</summary>
    /// <param name="retryAfterSeconds">The wait Slack asks for; null sends no header.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer ScriptRateLimit(int? retryAfterSeconds) => Script(_ =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{\"ok\":false,\"error\":\"ratelimited\"}", Encoding.UTF8, "application/json") };
        if (retryAfterSeconds is { } seconds)
        {
            response.Headers.Add("Retry-After", seconds.ToString(CultureInfo.InvariantCulture));
        }

        return response;
    });

    /// <summary>Queues an application error: HTTP 200 with {"ok":false,"error":"code"}.</summary>
    /// <param name="code">Slack's error code ('invalid_auth', 'channel_not_found').</param>
    /// <returns>This server.</returns>
    public FakeSlackServer ScriptError(string code) => Script(_ => Json(new JsonObject { ["ok"] = false, ["error"] = code }));

    /// <summary>Queues a hostile answer: ok:false whose error text repeats the Authorization header the request carried.</summary>
    /// <returns>This server.</returns>
    public FakeSlackServer ScriptReflectToken() => Script(request => Json(new JsonObject
    {
        ["ok"] = false,
        ["error"] = "invalid_auth " + request.Headers.Authorization,
    }));

    /// <summary>Queues a canned status for the next request.</summary>
    /// <param name="status">The status.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer ScriptStatus(HttpStatusCode status) => Script(_ => new HttpResponseMessage(status) { Content = new StringContent("{}") });

    /// <summary>Queues a redirect to <paramref name="location"/>.</summary>
    /// <param name="location">Where it points.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer ScriptRedirect(string location) => Script(_ =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(location);
        return response;
    });

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uri = request.RequestUri!;
        _requests.Add(new RecordedRequest(request.Method, uri, request.Headers.Authorization?.ToString(), request.Headers.UserAgent.ToString()));
        var query = ParseQuery(uri.Query);
        var channel = Admit(uri, query);
        if (_script.Count > 0 && _script.Dequeue()(request) is { } scripted)
        {
            return Task.FromResult(scripted);
        }

        return Task.FromResult(uri.AbsolutePath.EndsWith("history", StringComparison.Ordinal)
            ? Paged(query, _store.Top(channel, query.TryGetValue("oldest", out var oldest) ? oldest : null, query.TryGetValue("latest", out var latest) ? latest : null))
            : Paged(query, _store.Thread(channel, query.GetValueOrDefault("ts", string.Empty))));
    }

    /// <summary>Fails the run (throws) for a host that is not Slack's, a route the fake does not know or a channel that is not allowed; returns the channel otherwise.</summary>
    private string Admit(Uri uri, Dictionary<string, string> query)
    {
        if (uri.Host is not ("slack.com" or "www.slack.com") && !uri.Host.EndsWith(".slack.com", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The fake Slack was asked for host {uri.Host}.");
        }

        if (uri.AbsolutePath is not ("/api/conversations.history" or "/api/conversations.replies"))
        {
            throw new InvalidOperationException($"The fake Slack was asked for an unexpected route: {uri.AbsolutePath}.");
        }

        return query.TryGetValue("channel", out var channel) && _allowed.Contains(channel)
            ? channel
            : throw new InvalidOperationException($"The fake Slack was asked for a channel that is not allowed: {query.GetValueOrDefault("channel")}.");
    }

    private HttpResponseMessage Paged(Dictionary<string, string> query, List<SlackMessage> messages)
    {
        var size = Math.Min(MaxPageSize, query.TryGetValue("limit", out var l) ? int.Parse(l, CultureInfo.InvariantCulture) : 100);
        var offset = query.TryGetValue("cursor", out var c) && c.StartsWith("off", StringComparison.Ordinal) ? int.Parse(c[3..], CultureInfo.InvariantCulture) : 0;
        var page = new JsonArray([.. messages.Skip(offset).Take(size).Select(ToJson)]);
        var more = offset + size < messages.Count;
        return Json(new JsonObject
        {
            ["ok"] = true,
            ["messages"] = page,
            ["has_more"] = more,
            ["response_metadata"] = new JsonObject { ["next_cursor"] = more ? "off" + (offset + size).ToString(CultureInfo.InvariantCulture) : string.Empty },
        });
    }

    private static JsonNode ToJson(SlackMessage m)
    {
        var o = new JsonObject { ["type"] = "message", ["ts"] = m.Ts, ["text"] = m.Text, ["user"] = "U0000SAMPLE" };
        if (m.Author is not null)
        {
            o["user_profile"] = new JsonObject { ["display_name"] = m.Author };
        }

        if (m.ThreadTs is not null)
        {
            o["thread_ts"] = m.ThreadTs;
        }

        if (m.ReplyCount > 0)
        {
            o["reply_count"] = m.ReplyCount;
        }

        if (m.LatestReply is not null)
        {
            o["latest_reply"] = m.LatestReply;
        }

        if (m.Subtype is not null)
        {
            o["subtype"] = m.Subtype;
        }

        return o;
    }

    private static HttpResponseMessage Json(JsonNode body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private static Dictionary<string, string> ParseQuery(string query)
        => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(static kv => kv.Split('=', 2))
            .ToDictionary(static kv => kv[0], static kv => Uri.UnescapeDataString(kv.Length > 1 ? kv[1] : string.Empty), StringComparer.Ordinal);
}
