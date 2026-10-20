using System.Globalization;
using System.Net;
using System.Text;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Api.Jobs;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// Serves a <see cref="FakeSindriApi"/> over HTTP the way Sindri's API writes it (docs/csharp/sindri.md: camelCase JSON,
/// server-sent events with id = sequence and event = type), so the real client runs against the same model as the fake.
/// The JSON is written by hand here, not by the app's own context, so a field-name mistake in the app shows; the
/// refusals are Sindri's own goldens served verbatim, and GoldenContractTests holds the hand-written bodies to the
/// goldens' shape (UI spec E-17).
/// </summary>
/// <param name="server">The model.</param>
internal sealed class FakeSindriHandler(FakeSindriApi server) : HttpMessageHandler
{
    /// <summary>The requests seen, in order: the path and the headers the app's identity rests on (API §6).</summary>
    public IReadOnlyList<SeenRequest> Requests => [.. _requests];

    private readonly List<SeenRequest> _requests = [];
    private readonly List<string> _reviewBodies = [];

    /// <summary>The raw bodies the review route received, as the client wrote them (so a test checks the wire, not a round trip).</summary>
    public IReadOnlyList<string> ReviewBodies
    {
        get
        {
            lock (_requests)
            {
                return [.. _reviewBodies];
            }
        }
    }

    /// <summary>When set, the token Sindri holds: a request without it is refused with 403 SND-AUTH-001, as Sindri's caller check does.</summary>
    public string? RequiredToken { get; set; }

    /// <summary>When set, every route answers this status with this body instead.</summary>
    public (HttpStatusCode Code, string Body)? Override { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Record(request);
        if (server.Down)
        {
            throw new HttpRequestException("Connection refused (127.0.0.1)");
        }

        if (Refused(request) is { } refused)
        {
            return Task.FromResult(refused);
        }

        if (Override is { } o)
        {
            return Task.FromResult(Text(o.Code, o.Body, "application/json"));
        }

        return Route(request, cancellationToken);
    }

    private void Record(HttpRequestMessage request)
    {
        lock (_requests)
        {
            _requests.Add(new SeenRequest(
                request.RequestUri?.AbsolutePath ?? string.Empty,
                Header(request, "X-Sindri-Caller"),
                Header(request, "X-Sindri-Client"),
                Header(request, "X-Sindri-Terminal"),
                Header(request, "Origin"),
                Header(request, "X-Sindri-Token"),
                Header(request, "X-Sindri-After"),
                request.Method.Method,
                request.RequestUri?.Query ?? string.Empty));
        }
    }

    private async Task<HttpResponseMessage> Route(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        var after = long.TryParse(Header(request, "X-Sindri-After"), NumberStyles.None, CultureInfo.InvariantCulture, out var carried) ? carried : 0;
        return uri.AbsolutePath switch
        {
            "/sindri/ping" => await PingAnswer(uri, cancellationToken),
            "/sindri/status" => await StatusAnswer(uri, after, cancellationToken),
            "/sindri/events" when string.Equals(Query(uri, "types"), InstanceChange.Type, StringComparison.Ordinal) => await Changes(Query(uri, "instance"), cancellationToken),
            "/sindri/events" => Events(After(uri), cancellationToken),
            "/sindri/commands" => Answer(Counted(await server.Commands(uri, cancellationToken), server.Catalog)),
            "/sindri/services" => Answer(Counted(await server.Services(uri, after), server.ServiceView)),
            "/sindri/views/rebuild" when request.Method == HttpMethod.Post => Answer(Counted(await server.RebuildViews(uri, cancellationToken), server.Rebuild)),
            "/sindri/queue" => Answer(Counted(await server.Queue(uri, after, cancellationToken), server.QueueView)),
            "/sindri/items" when request.Method == HttpMethod.Post =>
                Answer(Counted(await server.CreateItem(uri, await Body<CreateItemRequest>(request, cancellationToken), cancellationToken), server.Create)),
            var path when path.StartsWith("/sindri/items/", StringComparison.Ordinal) => await Item(request, path["/sindri/items/".Length..], after, cancellationToken),
            "/sindri/needs-you" when request.Method == HttpMethod.Get => Answer(Counted(await server.NeedsYou(uri, after, cancellationToken), server.NeedsYouView)),
            "/sindri/decisions" when request.Method == HttpMethod.Get => Answer(Counted(await server.Decisions(uri, Flag(uri, "pending"), Query(uri, "item"), after, cancellationToken), server.DecisionsAnswer(Flag(uri, "pending"), Query(uri, "item")))),
            var path when request.Method == HttpMethod.Post && path.StartsWith("/sindri/decisions/", StringComparison.Ordinal) =>
                Answer(Counted(await server.Decide(uri, Uri.UnescapeDataString(path["/sindri/decisions/".Length..]), await Body<DecideRequest>(request, cancellationToken), cancellationToken), server.DecideAnswer(Uri.UnescapeDataString(path["/sindri/decisions/".Length..])))),
            "/sindri/requests" when request.Method == HttpMethod.Post =>
                Answer(Counted(await server.FileRequest(uri, await Body<FileRequestRequest>(request, cancellationToken), cancellationToken), server.Filed)),
            "/sindri/requests" when request.Method == HttpMethod.Get => Answer(Counted(await server.Requests(uri, Query(uri, "state"), Query(uri, "item"), after, cancellationToken), server.RequestsAnswer(Query(uri, "item")))),
            // LANE ANCHOR U3': add the Needs you routes as arms above the workflow-read arm, each a RouteAnswer from the goldens. Insert immediately above this marker, never between the markers.
            // LANE ANCHOR U8: add the workflow command routes as arms above the workflow-read arm, each a RouteAnswer from the goldens. Insert immediately below this marker, never between the markers.
            "/sindri/workflow/recipes" when request.Method == HttpMethod.Get => Answer(Counted(await server.Recipes(uri, cancellationToken), server.RecipeListAnswer)),
            "/sindri/workflow/instances" when request.Method == HttpMethod.Post =>
                Answer(Counted(await server.StartInstance(uri, await Body<StartInstanceRequest>(request, cancellationToken), cancellationToken), server.StartAnswer)),

            { } path when path.StartsWith("/sindri/context/", StringComparison.Ordinal) => await ContextRoute(request, cancellationToken),
            // LANE ANCHOR U11a: the session reads, as arms above the workflow-read arm. Insert immediately below this marker, never between the markers.
            { } path when request.Method == HttpMethod.Get && (path == "/sindri/sessions" || path.StartsWith("/sindri/sessions/", StringComparison.Ordinal)) => await Sessions(uri, cancellationToken),
            { } path when path == "/sindri/jobs" || path.StartsWith("/sindri/jobs/", StringComparison.Ordinal) => await Jobs(request, cancellationToken),
            { } path when request.Method == HttpMethod.Get && path.StartsWith("/sindri/workflow/", StringComparison.Ordinal) => await Workflow(uri, cancellationToken),
            _ => Text(HttpStatusCode.NotFound, "{}", "application/json"),
        };
    }

    // The context routes (context spec §8): the call goes through the model (its counts, its refusals, the queue it keeps), then the
    // answer the model holds is served as it is. A key, an anchor or a path is only ever a query value.
    private async Task<HttpResponseMessage> ContextRoute(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        if (request.Method == HttpMethod.Post && uri.AbsolutePath.EndsWith("/map/review", StringComparison.Ordinal))
        {
            var sent = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false); // why: the client always sends a body on this POST
            lock (_requests)
            {
                _reviewBodies.Add(sent);
            }

            _ = await server.ReviewDecision(uri, await Body<ReviewDecisionRequest>(request, cancellationToken), cancellationToken);
            return Answer(server.ReviewDecided);
        }

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var limit = int.TryParse(query["limit"], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : (int?)null;
        switch (uri.AbsolutePath["/sindri/context/".Length..])
        {
            case "lenses":
                var lenses = Lenses(query);
                _ = await server.Lenses(uri, lenses, cancellationToken);
                return Answer(server.LensesAnswer(lenses));
            case "filemap":
                var files = new FileMapQuery(query["repo"] ?? string.Empty, Path: query["path"], UnmappedOnly: query["unmapped"] == "true", Draft: query["draft"] == "true");
                _ = await server.FileMap(uri, files, cancellationToken);
                return Answer(server.FileMapFor(files));
            case "map/review":
                _ = await server.ReviewQueue(uri, limit, cancellationToken);
                return Answer(server.ReviewQueueBody());
            case "map/review/history":
                _ = await server.ReviewHistory(uri, limit, cancellationToken);
                return Answer(server.ReviewHistoryAnswer);
            default:
                return await ContextRecord(uri, query, cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> ContextRecord(Uri uri, System.Collections.Specialized.NameValueCollection query, CancellationToken cancellationToken)
    {
        switch (uri.AbsolutePath["/sindri/context/".Length..])
        {
            case "map":
                _ = await server.MapStatus(uri, query["anchor"], cancellationToken);
                return Answer(server.MapStatusAnswer);
            case "node":
                _ = await server.Node(uri, query["key"] ?? string.Empty, cancellationToken);
                return Answer(server.NodeAnswer);
            case "sources":
                _ = await server.Sources(uri, cancellationToken);
                return Answer(server.SourcesAnswer);
            default:
                return Text(HttpStatusCode.NotFound, "{}", "application/json");
        }
    }

    private static LensesQuery Lenses(System.Collections.Specialized.NameValueCollection query) =>
        new([.. query.GetValues("anchor") ?? []], Depth: int.TryParse(query["depth"], NumberStyles.None, CultureInfo.InvariantCulture, out var depth) ? depth : null, Draft: query["draft"] == "true", MapVersion: query["mapVersion"]);

    // The workflow reads (API §3.8): the call goes through the model (so a down server or a refusal answers as it does there),
    // then the route's golden is served as the model has it. An address is only ever a query value, so every segment is an id, a key or a recipe.
    private async Task<HttpResponseMessage> Workflow(Uri uri, CancellationToken cancellationToken)
    {
        var parts = uri.AbsolutePath["/sindri/workflow/".Length..].Split('/').Select(Uri.UnescapeDataString).ToArray();
        switch (parts)
        {
            case ["instances"]:
                var query = Query(uri);
                _ = await server.Instances(uri, query, cancellationToken);
                return Answer(server.InstancesAnswer(query));
            case ["instances", var id]:
                _ = await server.Instance(uri, id, cancellationToken);
                return Answer(server.InstanceAnswer(id));
            case ["instances", var id, "facts", var key]:
                _ = await server.InstanceFact(uri, id, key, cancellationToken);
                return Answer(server.FactAnswer(id, key));
            case ["recipes", var named]:
                var at = named.IndexOf('@', StringComparison.Ordinal);
                var (recipe, hash) = at < 0 ? (named, null) : (named[..at], named[(at + 1)..]);
                _ = await server.Recipe(uri, recipe, hash, cancellationToken);
                return Answer(server.RecipeAnswer(recipe, hash));
            default:
                return Text(HttpStatusCode.NotFound, "{}", "application/json");
        }
    }

    // The session reads (collector spec CA-1, CA-2): through the model (a down server or a refusal answers as it does there), then the route's golden as the model has it.
    private async Task<HttpResponseMessage> Sessions(Uri uri, CancellationToken cancellationToken)
    {
        var parts = uri.AbsolutePath["/sindri/sessions".Length..].Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();
        switch (parts)
        {
            case []:
                var query = new SessionQuery(Query(uri, "provider"), Query(uri, "state"), Query(uri, "repo"), Flag(uri, "inWorkflow"));
                _ = await server.Sessions(uri, query, cancellationToken);
                return Answer(server.SessionListAnswer());
            case [var provider, var id]:
                _ = await server.Session(uri, provider, id, cancellationToken);
                return Answer(server.SessionOneAnswer(provider, id));
            default:
                return Text(HttpStatusCode.NotFound, "{}", "application/json");
        }
    }

    private async Task<HttpResponseMessage> Jobs(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        var parts = uri.AbsolutePath["/sindri/jobs".Length..].Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();
        switch (parts)
        {
            case [] when request.Method == HttpMethod.Get:
                _ = await server.Jobs(uri, new JobQuery(Query(uri, "state"), Query(uri, "kind")), cancellationToken);
                return Answer(server.JobsAnswer);
            case [var id] when request.Method == HttpMethod.Get:
                _ = await server.Job(uri, id, cancellationToken);
                return Answer(server.JobOneAnswer(id));
            case [var id, "cancel"] when request.Method == HttpMethod.Post:
                return Answer(Counted(await server.CancelJob(uri, id, cancellationToken), server.CancelJobAnswer));
            default:
                return Text(HttpStatusCode.NotFound, "{}", "application/json");
        }
    }

    private static InstanceQuery Query(Uri uri)
    {
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return new InstanceQuery(
            Root: query["root"] == "true",
            Open: query["open"] == "true",
            Recipe: query["recipe"],
            Subject: query["subject"],
            Address: query["address"]);
    }

    // The ping goes through the model (its count, and the new epoch that drops a pending sequence); a failing ping is a transport failure.
    private async Task<HttpResponseMessage> PingAnswer(Uri uri, CancellationToken cancellationToken)
    {
        var ping = await server.Ping(uri, cancellationToken);
        return server.FailPing || ping.IsError
            ? throw new HttpRequestException("Connection refused (127.0.0.1)")
            : Text(HttpStatusCode.OK, Ping(ping.Value), "application/json");
    }

    // The model answers with the header the request carried; a status that can't be reached is a transport failure, as the client sees it.
    private async Task<HttpResponseMessage> StatusAnswer(Uri uri, long after, CancellationToken cancellationToken)
    {
        var status = await server.Status(uri, after, cancellationToken);
        return server.FailStatus
            ? throw new HttpRequestException("Connection refused (127.0.0.1)")
            : status.IsError
                ? Answer(RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, status.Error.Code, status.Error.Message))
                : Text(HttpStatusCode.OK, Status(status.Value), "application/json");
    }

    // /sindri/items/{id}, /sindri/items/{id}/timeline and POST /sindri/items/{id}/close (API P3).
    private async Task<HttpResponseMessage> Item(HttpRequestMessage request, string rest, long sequence, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        var parts = rest.Split('/');
        return parts switch
        {
            [var id] => Answer(Counted(await server.Item(uri, id, sequence, cancellationToken), server.ItemAnswer(id))),
            [var id, "timeline"] => Answer(Counted(await server.Timeline(uri, id, After(uri), sequence, cancellationToken), server.TimelineAnswer(id, After(uri)))),
            [var id, "close"] when request.Method == HttpMethod.Post =>
                Answer(Counted(await server.CloseItem(uri, id, await Body<CloseItemRequest>(request, cancellationToken), cancellationToken), server.Close)),
            _ => Text(HttpStatusCode.NotFound, "{}", "application/json"),
        };
    }

    private static async Task<T> Body<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false); // why: the client always sends a body on these POSTs
        return Kernel.Json.AwJson.Deserialize<T>(bytes, SindriApiClient.Json).Value;
    }

    // Sindri's caller check (API P1 §6: a human without this start's token, ping, status and the stream included), then the model's own refusal: 403 with ProblemDetails.
    private HttpResponseMessage? Refused(HttpRequestMessage request)
    {
        var sent = Header(request, "X-Sindri-Token");
        if (RequiredToken is { } held && !string.Equals(held, sent, StringComparison.Ordinal))
        {
            return Answer(new RouteAnswer(HttpStatusCode.Forbidden, GoldenFiles.Api("status", sent is null ? "human-no-token" : "human-stale-token")));
        }

        return server.Refusal is { } refusal ? Answer(RouteAnswer.Problem(HttpStatusCode.Forbidden, refusal.Code, refusal.Detail)) : null;
    }

    // The route's golden text as served; the call above went through the model so its counts and gates hold. The one answer
    // the model decides is "views behind" (SND-API-011, a read that carried a sequence the views haven't reached).
    private static RouteAnswer Counted<T>(Kernel.Result<T> result, RouteAnswer answer) =>
        result.IsError && string.Equals(result.Error.Code, SindriApiClient.ViewsBehind, StringComparison.Ordinal)
            ? RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, result.Error.Code, result.Error.Message)
            : answer;

    // A success is JSON; anything else is ProblemDetails, as Sindri's ApiProblems writes it.
    private static HttpResponseMessage Answer(RouteAnswer answer) =>
        Text(answer.Status, answer.Body, answer.IsSuccess ? "application/json" : "application/problem+json");

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(',', values) : null;

    private static string? Query(Uri uri, string name) =>
        uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)).Where(p => p.Length == 2 && p[0] == name).Select(p => Uri.UnescapeDataString(p[1])).FirstOrDefault();

    private static bool? Flag(Uri uri, string name) => Query(uri, name) is { } v ? string.Equals(v, "true", StringComparison.Ordinal) : null;

    private static long After(Uri uri) =>
        uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=')).Where(p => p is ["after", _]).Select(p => long.Parse(p[1], CultureInfo.InvariantCulture)).FirstOrDefault();

    private HttpResponseMessage Events(long after, CancellationToken cancellationToken)
    {
        var body = new ChunkStream();
        _ = Task.Run(async () =>
        {
            await foreach (var e in server.Events(new Uri("http://fake/"), after, cancellationToken))
            {
                body.Send(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"id: {e.Sequence}\nevent: {e.Type}\ndata: {Event(e)}\n\n")));
            }

            body.End();
        }, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) { Headers = { { "Content-Type", "text/event-stream" } } } };
    }

    // The derived stream: frames are written as the API writes them, with an event name and no "id:" line (API §5).
    private async Task<HttpResponseMessage> Changes(string? instance, CancellationToken cancellationToken)
    {
        var body = new ChunkStream();
        var items = server.InstanceChanges(new Uri("http://fake/"), instance, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var open = await items.MoveNextAsync();   // the stream is registered with the model before the response, as a connected socket is
        _ = Task.Run(async () =>
        {
            for (var more = open; more; more = await items.MoveNextAsync())
            {
                if (items.Current.Change is { } c)
                {
                    body.Send(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"event: {InstanceChange.Type}\ndata: {{\"instance\":\"{c.Instance}\",\"state\":\"{c.State}\",\"cursor\":{c.Cursor}}}\n\n")));
                }
            }

            await items.DisposeAsync();
            body.End();
        }, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) { Headers = { { "Content-Type", "text/event-stream" } } } };
    }

    private static HttpResponseMessage Text(HttpStatusCode code, string body, string type) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, type) };

    /// <summary>A ping body as this handler writes it.</summary>
    internal static string Ping(PingView p) => string.Create(CultureInfo.InvariantCulture, $$"""{"pid":{{p.Pid}},"epoch":{{p.Epoch}}}""");

    /// <summary>An event's data line as this handler writes it: the PublicEvent envelope (API spec §5.3), an empty payload.</summary>
    internal static string Event(EventView e) => string.Create(CultureInfo.InvariantCulture,
        $$"""{"sequence":{{e.Sequence}},"id":"{{Id(e.Sequence * 2)}}","type":"{{e.Type}}","schemaVersion":1,"occurredAt":"{{Time(e.OccurredAt)}}","actor":"sindri","tenant":"local","ring":0,"item":null,"correlationId":"{{Id((e.Sequence * 2) + 1)}}","causationId":null,"traceId":null,"payload":{{EmptyObject}}}""");

    private const string EmptyObject = "{}";

    // The PublicEvent envelope's ids are Guids; the app never reads them, so a number-derived one is enough.
    private static string Id(long n) => "00000000-0000-0000-0000-" + n.ToString("D12", CultureInfo.InvariantCulture);

    /// <summary>A status body as this handler writes it.</summary>
    internal static string Status(StatusView s) => string.Create(CultureInfo.InvariantCulture,
        $$"""{"running":{{Bool(s.Running)}},"pid":{{s.Pid}},"epoch":{{s.Epoch}},"leaseExpiresAt":{{Quoted(s.LeaseExpiresAt)}},"profile":{"approved":{{Quoted(s.Profile.Approved)}},"live":{{Quoted(s.Profile.Live)}},"changedSinceApproval":{{Bool(s.Profile.ChangedSinceApproval)}},"problem":{{Quoted(s.Profile.Problem)}},"source":{{Quoted(s.Profile.Source)}},"needsApproval":{{Bool(s.Profile.NeedsApproval)}}},"services":[{{string.Join(',', s.Services.Select(Service))}}]{{ApiVersion(s.ApiVersion)}}{{Views(s.Views)}}{{Health(s.Health)}}{{Sessions(s.Sessions)}}}""");

    // Absent before API P1 (the field doesn't exist on S1's status), so the app must read a status without it.
    private static string ApiVersion(int? version) => version is { } v ? string.Create(CultureInfo.InvariantCulture, $",\"apiVersion\":{v}") : string.Empty;

    // Null until API P2's projector fills them; the status always carries both fields, as Sindri's does. Written with P2's full shape (text, clause, sentence).
    private static string Views(AsOfView? v) => v is null ? ",\"views\":null" : string.Create(CultureInfo.InvariantCulture,
        $$""","views":{"cursor":{{v.Cursor}},"ledgerHead":{{v.LedgerHead}},"projectedAt":{{Quoted(v.ProjectedAt)}},"rebuilding":{{Bool(v.Rebuilding)}},"stalled":{{Bool(v.Stalled)}},"stalledAt":{{Number(v.StalledAt)}},"stalledType":{{Quoted(v.StalledType)}},"text":{{Quoted(v.Text)}}}""");

    private static string Health(HealthView? h) => h is null ? ",\"health\":null" :
        $$""","health":{"word":"{{h.Word}}","problems":[{{string.Join(',', h.Problems.Select(p => $$"""{"code":"{{p.Code}}","text":"{{p.Text}}","clause":{{Quoted(p.Clause)}}}"""))}}],"sentence":{{Quoted(h.Sentence)}}}""";

    // Absent before CO-3 (the spec-shaped status goldens carry none), so a null count is left out.
    private static string Sessions(SessionCountsView? c) => c is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $",\"sessions\":{{\"live\":{c.Live},\"asking\":{c.Asking},\"problems\":{c.Problems}}}");

    private static string Service(ServiceView v) => $$"""{"name":"{{v.Name}}","every":"{{v.Every}}","lastRun":{{Quoted(v.LastRun)}}}""";

    private static string Number(long? n) => n is { } v ? v.ToString(CultureInfo.InvariantCulture) : "null";

    private static string Bool(bool b) => b ? "true" : "false";

    private static string Quoted(string? s) => s is null ? "null" : $"\"{s}\"";

    private static string Quoted(DateTimeOffset? t) => t is { } v ? $"\"{Time(v)}\"" : "null";

    private static string Time(DateTimeOffset t) => t.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
