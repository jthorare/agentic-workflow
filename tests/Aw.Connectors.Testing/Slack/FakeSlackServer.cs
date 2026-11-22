using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Testing.GitHub;

namespace Aw.Connectors.Testing.Slack;

/// <summary>
/// A fake Slack Web API: an <see cref="HttpMessageHandler"/> that serves what was added the way Slack does (HTTP 200 with {"ok":true,…} or {"ok":false,"error":…}, a
/// response_metadata.next_cursor to page on). It answers the index reads ('conversations.history', 'conversations.replies') and the Messages Release 1 reads
/// (messages spec §5.1, §8.1): 'auth.test', 'users.conversations', 'conversations.info', 'users.info', 'emoji.list' and 'search.messages'. Arguments come from the
/// query or a form body. Scripted answers come first: a revoked token answers every method with ok:false; a 429 with Retry-After can be queued for one method or
/// for the next request (also ok:false, a redirect, a reply that reflects the token). It records every request and FAILS the run (throws) on a route it does not
/// know, a history or replies read of a channel that is not allowed, or a search modifier it does not know. A conversation added with
/// <see cref="AddConversation"/> is allowed. It is safe to call from several threads, so Aw.Connectors.Testing.Server can serve it over loopback HTTP.
/// </summary>
public sealed class FakeSlackServer(params string[] allowedChannels) : HttpMessageHandler
{
    /// <summary>The scopes 'auth.test' reports in its x-oauth-scopes header: Release 1's user scopes (messages spec §5.1).</summary>
    public const string ReleaseOneScopes = "channels:read,groups:read,im:read,mpim:read,channels:history,groups:history,im:history,mpim:history,users:read,emoji:read,search:read";

    private static readonly Dictionary<string, Func<FakeSlackServer, Dictionary<string, string>, HttpResponseMessage>> _methods = new(StringComparer.Ordinal)
    {
        ["auth.test"] = static (s, _) => s.AuthTest(),
        ["users.conversations"] = static (s, q) => s.UsersConversations(q),
        ["conversations.history"] = static (s, q) => s.History(q),
        ["conversations.replies"] = static (s, q) => s.Replies(q),
        ["conversations.info"] = static (s, q) => s.ConversationInfo(q),
        ["users.info"] = static (s, q) => s.UserInfo(q),
        ["emoji.list"] = static (s, _) => s.EmojiList(),
        ["search.messages"] = static (s, q) => FakeSlackSearch.Answer(s, q),
    };

    private static readonly HashSet<string> _channelReads = new(["conversations.history", "conversations.replies"], StringComparer.Ordinal);

    private readonly Lock _gate = new();
    private readonly HashSet<string> _allowed = new(allowedChannels ?? [], StringComparer.Ordinal);
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage?>> _script = new();
    private readonly Dictionary<string, Queue<int?>> _methodLimits = new(StringComparer.Ordinal);
    private readonly List<SlackConversationSeed> _conversations = [];
    private readonly Dictionary<string, SlackUserSeed> _users = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _emoji = new(StringComparer.Ordinal);
    private readonly List<RecordedRequest> _requests = [];
    private string? _revoked;

    /// <summary>The most items the server serves a page, whatever limit asks.</summary>
    public int MaxPageSize { get; set; } = 200;

    /// <summary>Who the token belongs to, as 'auth.test' answers it.</summary>
    public SlackIdentity Identity { get; set; } = new("T0000FIX", "Fixture Workspace", "fixture", "U0000ME", "fixture-me");

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    internal SlackStore Store { get; } = new();

    internal IEnumerable<SlackUserSeed> Users => _users.Values;

    /// <summary>Adds a message to a channel (a reply carries its root's ts as thread_ts).</summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="message">The message.</param>
    /// <param name="userId">The user id that wrote it (Slack's 'user'); null writes U0000SAMPLE.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer Add(string channelId, SlackMessage message, string? userId = null)
    {
        lock (_gate)
        {
            Store.Add(channelId, message, userId);
        }

        return this;
    }

    /// <summary>Adds a conversation 'users.conversations' lists and 'conversations.info' describes; its history and replies may be read.</summary>
    /// <param name="conversation">The conversation.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer AddConversation(SlackConversationSeed conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        lock (_gate)
        {
            _conversations.Add(conversation);
            _ = _allowed.Add(conversation.Id);
        }

        return this;
    }

    /// <summary>Adds a person 'users.info' returns.</summary>
    /// <param name="user">The person.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer AddUser(SlackUserSeed user)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (_gate)
        {
            _users[user.Id] = user;
        }

        return this;
    }

    /// <summary>Adds a custom emoji 'emoji.list' returns.</summary>
    /// <param name="name">The emoji name, without colons.</param>
    /// <param name="value">Its image address, or 'alias:other' for an alias.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer AddEmoji(string name, string value)
    {
        lock (_gate)
        {
            _emoji[name] = value;
        }

        return this;
    }

    /// <summary>Seeds the made-up workspace of <see cref="SlackWorkspaceSeed"/>: people, a DM, a group DM, channels, a mention and a thread with replies.</summary>
    /// <returns>This server.</returns>
    public FakeSlackServer SeedWorkspace() => SlackWorkspaceSeed.Apply(this);

    /// <summary>Whether the fake serves <paramref name="method"/> ('conversations.history'), so a caller can check a name before scripting it.</summary>
    /// <param name="method">The Web API method.</param>
    /// <returns>True when the fake answers it.</returns>
    public static bool Serves(string method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return _methods.ContainsKey(method);
    }

    /// <summary>Revokes the token: from now on every method answers HTTP 200 with {"ok":false,"error":<paramref name="error"/>}, before any script.</summary>
    /// <param name="error">Slack's error code; 'invalid_auth' by default ('token_revoked' is the other one Slack sends).</param>
    /// <returns>This server.</returns>
    public FakeSlackServer Revoke(string error = "invalid_auth")
    {
        lock (_gate)
        {
            _revoked = error;
        }

        return this;
    }

    /// <summary>Queues a scripted answer for the next request; returning null lets the request through to the normal behavior.</summary>
    /// <param name="respond">Builds the answer.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer Script(Func<HttpRequestMessage, HttpResponseMessage?> respond)
    {
        lock (_gate)
        {
            _script.Enqueue(respond);
        }

        return this;
    }

    /// <summary>Lets the next request through unchanged.</summary>
    /// <returns>This server.</returns>
    public FakeSlackServer ScriptThrough() => Script(_ => null);

    /// <summary>Queues Slack's rate-limit answer for the next request: HTTP 429 with a Retry-After.</summary>
    /// <param name="retryAfterSeconds">The wait Slack asks for; null sends no header.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer ScriptRateLimit(int? retryAfterSeconds) => Script(_ => RateLimited(retryAfterSeconds));

    /// <summary>Queues Slack's rate-limit answer for the next <paramref name="times"/> requests of one method only; the other methods answer as usual.</summary>
    /// <param name="method">The method ('conversations.history').</param>
    /// <param name="retryAfterSeconds">The wait Slack asks for; null sends no header.</param>
    /// <param name="times">How many of that method's requests get the 429.</param>
    /// <returns>This server.</returns>
    public FakeSlackServer ScriptRateLimit(string method, int? retryAfterSeconds, int times = 1)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(times);
        if (!_methods.ContainsKey(method))
        {
            throw new ArgumentException($"The fake Slack does not serve {method}.", nameof(method));
        }

        lock (_gate)
        {
            if (!_methodLimits.TryGetValue(method, out var limits))
            {
                limits = new Queue<int?>();
                _methodLimits[method] = limits;
            }

            for (var i = 0; i < times; i++)
            {
                limits.Enqueue(retryAfterSeconds);
            }
        }

        return this;
    }

    /// <summary>Queues an application error: HTTP 200 with {"ok":false,"error":"code"}.</summary>
    /// <param name="code">Slack's error code ('invalid_auth', 'channel_not_found').</param>
    /// <returns>This server.</returns>
    public FakeSlackServer ScriptError(string code) => Script(_ => Error(code));

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

    internal SlackConversationSeed? Conversation(string id) => _conversations.Find(c => c.Id == id);

    internal SlackUserSeed? User(string id) => _users.GetValueOrDefault(id);

    internal static HttpResponseMessage Json(JsonNode body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    internal static HttpResponseMessage Error(string code) => Json(new JsonObject { ["ok"] = false, ["error"] = code });

    internal (JsonArray Items, string Next) Slice<T>(Dictionary<string, string> query, List<T> all, Func<T, JsonNode> toJson, int defaultSize = 100)
    {
        var size = Math.Min(MaxPageSize, query.TryGetValue("limit", out var l) ? int.Parse(l, CultureInfo.InvariantCulture) : defaultSize);
        var offset = query.TryGetValue("cursor", out var c) && c.StartsWith("off", StringComparison.Ordinal) ? int.Parse(c[3..], CultureInfo.InvariantCulture) : 0;
        var more = offset + size < all.Count;
        return (new JsonArray([.. all.Skip(offset).Take(size).Select(toJson)]), more ? "off" + (offset + size).ToString(CultureInfo.InvariantCulture) : string.Empty);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = ParseQuery(request.RequestUri!.Query);
        if (request.Content is { } form && string.Equals(form.Headers.ContentType?.MediaType, "application/x-www-form-urlencoded", StringComparison.Ordinal))
        {
            foreach (var (key, value) in ParseQuery(await form.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)))
            {
                query[key] = value;
            }
        }

        lock (_gate)
        {
            return Answer(request, query);
        }
    }

    private HttpResponseMessage Answer(HttpRequestMessage request, Dictionary<string, string> query)
    {
        var uri = request.RequestUri!;
        _requests.Add(new RecordedRequest(request.Method, uri, request.Headers.Authorization?.ToString(), request.Headers.UserAgent.ToString()));
        var method = Admit(uri, query);
        if (_revoked is { } revoked)
        {
            return Error(revoked);
        }

        if (_methodLimits.TryGetValue(method, out var limits) && limits.Count > 0)
        {
            return RateLimited(limits.Dequeue());
        }

        if (_script.Count > 0 && _script.Dequeue()(request) is { } scripted)
        {
            return scripted;
        }

        return _methods[method](this, query);
    }

    /// <summary>Fails the run (throws) for a host that is not Slack's, a route the fake does not know or a history or replies read of a channel that is not allowed; returns the method otherwise.</summary>
    private string Admit(Uri uri, Dictionary<string, string> query)
    {
        if (uri.Host is not ("slack.com" or "www.slack.com") && !uri.Host.EndsWith(".slack.com", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The fake Slack was asked for host {uri.Host}.");
        }

        var method = uri.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal) ? uri.AbsolutePath[5..] : string.Empty;
        if (!_methods.ContainsKey(method))
        {
            throw new InvalidOperationException($"The fake Slack was asked for an unexpected route: {uri.AbsolutePath}.");
        }

        if (_channelReads.Contains(method) && !(query.TryGetValue("channel", out var channel) && _allowed.Contains(channel)))
        {
            throw new InvalidOperationException($"The fake Slack was asked for a channel that is not allowed: {query.GetValueOrDefault("channel")}.");
        }

        return method;
    }

    private HttpResponseMessage AuthTest()
    {
        var response = Json(new JsonObject
        {
            ["ok"] = true,
            ["url"] = "https://" + Identity.Domain + ".slack.com/",
            ["team"] = Identity.Team,
            ["user"] = Identity.User,
            ["team_id"] = Identity.TeamId,
            ["user_id"] = Identity.UserId,
        });
        _ = response.Headers.TryAddWithoutValidation("x-oauth-scopes", ReleaseOneScopes);
        return response;
    }

    private HttpResponseMessage UsersConversations(Dictionary<string, string> query)
    {
        var types = query.GetValueOrDefault("types", "public_channel").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hideArchived = IsTrue(query, "exclude_archived");
        var listed = _conversations.Where(c => c.IsMember && types.Contains(TypeOf(c.Kind), StringComparer.Ordinal) && !(hideArchived && c.IsArchived)).ToList();
        var (page, next) = Slice(query, listed, c => SlackConversationJson.Of(c, info: false));
        return Json(new JsonObject { ["ok"] = true, ["channels"] = page, ["response_metadata"] = new JsonObject { ["next_cursor"] = next } });
    }

    private HttpResponseMessage History(Dictionary<string, string> query)
    {
        var channel = query["channel"];
        return MessagesPage(query, channel, Store.Top(channel, query.GetValueOrDefault("oldest"), query.GetValueOrDefault("latest")));
    }

    private HttpResponseMessage Replies(Dictionary<string, string> query)
    {
        var channel = query["channel"];
        return MessagesPage(query, channel, Store.Thread(channel, query.GetValueOrDefault("ts", string.Empty), query.GetValueOrDefault("oldest"), IsTrue(query, "inclusive")));
    }

    private HttpResponseMessage MessagesPage(Dictionary<string, string> query, string channel, List<SlackMessage> messages)
    {
        var (page, next) = Slice(query, messages, m => SlackMessageJson.Of(m, Store.UserOf(channel, m.Ts)));
        return Json(new JsonObject
        {
            ["ok"] = true,
            ["messages"] = page,
            ["has_more"] = next.Length > 0,
            ["response_metadata"] = new JsonObject { ["next_cursor"] = next },
        });
    }

    private HttpResponseMessage ConversationInfo(Dictionary<string, string> query)
        => Conversation(query.GetValueOrDefault("channel", string.Empty)) is { } conversation
            ? Json(new JsonObject { ["ok"] = true, ["channel"] = SlackConversationJson.Of(conversation, info: true) })
            : Error("channel_not_found");

    private HttpResponseMessage UserInfo(Dictionary<string, string> query)
        => User(query.GetValueOrDefault("user", string.Empty)) is { } user
            ? Json(new JsonObject { ["ok"] = true, ["user"] = SlackUserJson.Of(user, Identity.TeamId) })
            : Error("user_not_found");

    private HttpResponseMessage EmojiList()
    {
        var emoji = new JsonObject();
        foreach (var (name, value) in _emoji.OrderBy(static e => e.Key, StringComparer.Ordinal))
        {
            emoji[name] = value;
        }

        return Json(new JsonObject { ["ok"] = true, ["emoji"] = emoji });
    }

    private static HttpResponseMessage RateLimited(int? retryAfterSeconds)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{\"ok\":false,\"error\":\"ratelimited\"}", Encoding.UTF8, "application/json") };
        if (retryAfterSeconds is { } seconds)
        {
            response.Headers.Add("Retry-After", seconds.ToString(CultureInfo.InvariantCulture));
        }

        return response;
    }

    private static string TypeOf(SlackConversationKind kind) => kind switch
    {
        SlackConversationKind.Private => "private_channel",
        SlackConversationKind.Im => "im",
        SlackConversationKind.Mpim => "mpim",
        SlackConversationKind.Public => "public_channel",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static bool IsTrue(Dictionary<string, string> query, string key)
        => query.TryGetValue(key, out var value) && (string.Equals(value, "true", StringComparison.Ordinal) || string.Equals(value, "1", StringComparison.Ordinal));

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(static kv => kv.Split('=', 2)))
        {
            pairs[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv.Length > 1 ? kv[1].Replace('+', ' ') : string.Empty);
        }

        return pairs;
    }
}
