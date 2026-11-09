using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Handoff;

namespace Aw.Sindri.Testing.Handoff;

/// <summary>
/// The contract's HTTP side on 127.0.0.1 over a <see cref="FakeInbox"/>, for HttpInboxClient's tests: GET /offers, POST /offers/{id}/claim and
/// POST /offers/{id}/result with the bodies and statuses of schemas/handoff/inbox.openapi.yaml. A test can make a route fail a few times
/// (<see cref="FailNext"/>) or answer a body of its own (<see cref="ReplyNext"/>).
/// </summary>
public sealed class FakeInboxServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Lock _gate = new();
    private readonly List<string> _requests = [];
    private readonly List<(string Route, int Status, int Left)> _failures = [];
    private readonly List<(string Route, string Json)> _replies = [];

    /// <summary>Initializes a new instance of the <see cref="FakeInboxServer"/> class and starts serving on a free loopback port.</summary>
    /// <param name="backing">The inbox whose state it serves.</param>
    public FakeInboxServer(FakeInbox backing)
    {
        Backing = backing ?? throw new ArgumentNullException(nameof(backing));
        var (listener, baseUri) = LoopbackListener.Start();
        _listener = listener;
        Url = baseUri;
        _ = Serve();
    }

    /// <summary>Gets the inbox whose state it serves.</summary>
    public FakeInbox Backing { get; }

    /// <summary>Gets the base URL.</summary>
    public Uri Url { get; }

    /// <summary>Gets the requests received, as 'METHOD /path' (no query), in order.</summary>
    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Answers the next <paramref name="times"/> requests to a route with a fixed status before serving normally.</summary>
    /// <param name="route">'METHOD /path-prefix'.</param>
    /// <param name="status">The status to answer.</param>
    /// <param name="times">How many requests.</param>
    public void FailNext(string route, int status, int times)
    {
        lock (_gate)
        {
            _failures.Add((route, status, times));
        }
    }

    /// <summary>Answers the next request to a route with a body instead of the real one.</summary>
    /// <param name="route">'METHOD /path-prefix'.</param>
    /// <param name="json">The body, sent with status 200.</param>
    public void ReplyNext(string route, string json)
    {
        lock (_gate)
        {
            _replies.Add((route, json));
        }
    }

    /// <inheritdoc />
    public void Dispose() => _listener.Close();

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(CancellationToken.None);
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(CancellationToken.None);
            await Route(context, body);
        }
    }

    private async Task Route(HttpListenerContext context, string body)
    {
        var path = context.Request.Url!.AbsolutePath;
        var route = context.Request.HttpMethod + " " + path;
        if (Scripted(route) is { } scripted)
        {
            Reply(context, scripted.Status, scripted.Json ?? "{}");
            return;
        }

        var bearer = context.Request.Headers["Authorization"] is { } h && h.StartsWith("Bearer ", StringComparison.Ordinal) ? h["Bearer ".Length..] : string.Empty;
        var key = new TokenSet(bearer, RefreshToken: null, ExpiresAt: null);
        var segments = path.Trim('/').Split('/');
        try
        {
            switch (context.Request.HttpMethod, segments)
            {
                case ("GET", ["offers"]):
                    await List(context, key);
                    break;
                case ("POST", ["offers", var id, "claim"]):
                    await Claim(context, key, id, body);
                    break;
                case ("POST", ["offers", var offer, "result"]):
                    await Submit(context, key, offer, body);
                    break;
                default:
                    Reply(context, 404, "{}");
                    break;
            }
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Reply(context, 400, "{}");
        }
    }

    // A failure or a body a test scripted for this route, if any: it answers instead of the real inbox, once per use.
    private (int Status, string? Json)? Scripted(string route)
    {
        lock (_gate)
        {
            _requests.Add(route);
            var failure = _failures.FindIndex(f => route.StartsWith(f.Route, StringComparison.Ordinal) && f.Left > 0);
            if (failure >= 0)
            {
                var (r, status, left) = _failures[failure];
                _failures[failure] = (r, status, left - 1);
                return (status, null);
            }

            var reply = _replies.FindIndex(f => route.StartsWith(f.Route, StringComparison.Ordinal));
            if (reply < 0)
            {
                return null;
            }

            var json = _replies[reply].Json;
            _replies.RemoveAt(reply);
            return (200, json);
        }
    }

    private async Task List(HttpListenerContext context, TokenSet key)
    {
        var query = System.Web.HttpUtility.ParseQueryString(context.Request.Url!.Query);
        var target = new InboxTarget(Url, key, query["owner"] ?? string.Empty);
        Answer(context, await Backing.ListOffers(target, query["pageToken"], CancellationToken.None), static page => new JsonObject
        {
            ["offers"] = new JsonArray([.. page.Offers.Select(OfferJson)]),
            ["nextPageToken"] = page.NextPageToken,
        });
    }

    private async Task Claim(HttpListenerContext context, TokenSet key, string id, string body)
    {
        using var document = JsonDocument.Parse(body);
        var target = new InboxTarget(Url, key, document.RootElement.GetProperty("owner").GetString() ?? string.Empty);
        var claimed = await Backing.Claim(target, id, CancellationToken.None);
        if (claimed.IsError && claimed.Error.Code == SindriErrors.InboxOfferGone.Code && Backing.IsExpired(id))
        {
            Reply(context, 410, "{}");
            return;
        }

        Answer(context, claimed, static c => new JsonObject { ["claimId"] = c.ClaimKey, ["offer"] = OfferJson(c.Offer) });
    }

    private async Task Submit(HttpListenerContext context, TokenSet key, string offer, string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var result = new InboxResult(
            root.GetProperty("claimId").GetString()!,
            root.GetProperty("succeeded").GetBoolean(),
            root.GetProperty("markdown").GetString()!,
            [.. root.GetProperty("links").EnumerateArray().Select(static l => new HandoffLink(l.GetProperty("label").GetString()!, l.GetProperty("url").GetString()!))]);
        var target = new InboxTarget(Url, key, Backing.OwnerOf(offer) ?? string.Empty);
        Answer(context, await Backing.Submit(target, offer, result, CancellationToken.None), static _ => new JsonObject { ["accepted"] = true });
    }

    private static JsonObject OfferJson(Offer offer) => new()
    {
        ["id"] = offer.Key,
        ["title"] = offer.Title,
        ["repo"] = offer.Repo,
        ["branch"] = offer.Branch,
        ["prompt"] = offer.Prompt,
        ["links"] = new JsonArray([.. offer.Links.Select(static l => (JsonNode)new JsonObject { ["label"] = l.Label, ["url"] = l.Url })]),
        ["labels"] = new JsonArray([.. offer.Labels.Select(static l => (JsonNode)JsonValue.Create(l))]),
    };

    private static void Answer<T>(HttpListenerContext context, Result<T> result, Func<T, JsonObject> ok)
    {
        if (!result.IsError)
        {
            Reply(context, 200, ok(result.Value).ToJsonString());
            return;
        }

        var code = result.Error.Code;
        var status = code == SindriErrors.ConnectionNeedsSignIn.Code ? 401
            : code == SindriErrors.InboxClaimLost.Code || code == SindriErrors.InboxSubmitFailed.Code ? 409
            : code == SindriErrors.InboxOfferGone.Code ? 404
            : 400;
        Reply(context, status, "{}");
    }

    private static void Reply(HttpListenerContext context, int status, string json)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.OutputStream.Write(Encoding.UTF8.GetBytes(json));
        context.Response.Close();
    }
}
