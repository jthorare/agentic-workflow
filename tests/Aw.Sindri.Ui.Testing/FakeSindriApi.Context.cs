using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The context routes of the fake (U9a0): each answers the API's goldens, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private static readonly RouteAnswer _notServed = new(HttpStatusCode.NotFound, "{}");

    private static readonly DeskVerb[] _settling = [DeskVerb.Accept, DeskVerb.Reject, DeskVerb.Drop, DeskVerb.AcceptDeterministic];
    private readonly List<ReviewDecisionRequest> _reviewCalls = [];
    private readonly Dictionary<string, (int Index, JsonNode Entry)> _decidedEntries = new(StringComparer.Ordinal);
    private JsonObject? _queue;
    private int _contextCalls;
    private int _mapPublishCalls;

    /// <summary>GET /sindri/context/lenses's answer per query (the goldens by default).</summary>
    public Func<LensesQuery, RouteAnswer> LensesAnswer { get; set; } = ContextAnswers.Lenses;

    /// <summary>GET /sindri/items/{id}/scope's answer per item (the no-scope golden by default: nothing frozen yet).</summary>
    public Func<string, RouteAnswer> ItemScopeAnswer { get; set; } = _ => RouteAnswer.Api("items-id-scope", "no-scope");

    /// <summary>The items whose scope was asked for, in order.</summary>
    public IReadOnlyList<string> ScopeAsked { get { lock (_gate) { return [.. _scopeAsked]; } } }

    private readonly List<string> _scopeAsked = [];

    /// <summary>GET /sindri/context/map/whole's answer per query (the context-whole goldens by what was asked).</summary>
    public Func<WholeMapQuery, RouteAnswer> WholeMapAnswer { get; set; } = ContextAnswers.WholeMap;

    /// <summary>The whole-map reads asked, in order.</summary>
    public IReadOnlyList<WholeMapQuery> WholeMapAsked { get { lock (_gate) { return [.. _wholeAsked]; } } }

    private readonly List<WholeMapQuery> _wholeAsked = [];

    /// <summary>GET /sindri/context/filemap's answer, or null for the golden the query names.</summary>
    public RouteAnswer? FileMapAnswer { get; set; }

    /// <summary>GET /sindri/context/map/review's answer, or null for the queue as decisions have left it.</summary>
    public RouteAnswer? ReviewQueueAnswer { get; set; }

    /// <summary>POST /sindri/context/map/review's answer.</summary>
    public RouteAnswer ReviewDecided { get; set; } = RouteAnswer.Api("context-map-review", "post-accept-link");

    /// <summary>GET /sindri/context/map/review/history's answer.</summary>
    public RouteAnswer ReviewHistoryAnswer { get; set; } = RouteAnswer.Api("context-map-review-history", "after-accept-undo");

    /// <summary>GET /sindri/context/map's answer.</summary>
    public RouteAnswer MapStatusAnswer { get; set; } = RouteAnswer.Api("context-map", "map-tabs-anchor");

    /// <summary>GET /sindri/context/map/publish's answer (the Health page's Context map card).</summary>
    public RouteAnswer MapPublishAnswer { get; set; } = RouteAnswer.Api("context-map-publish", "published");

    /// <summary>How many times the publish state was read; counted apart from <see cref="ContextCalls"/>, which the Context page's tests hold at zero.</summary>
    public int MapPublishCalls => Volatile.Read(ref _mapPublishCalls);

    /// <summary>GET /sindri/context/node's answer.</summary>
    public RouteAnswer NodeAnswer { get; set; } = RouteAnswer.Api("context-node", "tab-manager");

    /// <summary>GET /sindri/context/sources's answer: not served until the context lane writes its golden.</summary>
    public RouteAnswer SourcesAnswer { get; set; } = _notServed;

    /// <summary>Held open until set: the lenses read waits for it (a read in flight); null answers at once. Cancelling the call completes it as cancelled.</summary>
    public TaskCompletionSource? LensesGate { get; set; }

    /// <summary>GET /sindri/context/code's answer: null serves the golden for a known anchor and SND-CONTEXT-003 for another.</summary>
    public RouteAnswer? CodeAnswer { get; set; }

    /// <summary>Held open until set: the code lens read waits for it.</summary>
    public TaskCompletionSource? CodeGate { get; set; }

    /// <summary>Held open until set: the file map read waits for it.</summary>
    public TaskCompletionSource? FileMapGate { get; set; }

    /// <summary>Held open until set: the review queue read waits for it.</summary>
    public TaskCompletionSource? ReviewQueueGate { get; set; }

    /// <summary>Held open until set: the review history read waits for it.</summary>
    public TaskCompletionSource? ReviewHistoryGate { get; set; }

    /// <summary>Held open until set: the map status read waits for it.</summary>
    public TaskCompletionSource? MapStatusGate { get; set; }

    /// <summary>Held open until set: the node read waits for it.</summary>
    public TaskCompletionSource? NodeGate { get; set; }

    /// <summary>Held open until set: the sources read waits for it.</summary>
    public TaskCompletionSource? SourcesGate { get; set; }

    /// <summary>Every review decision received, in order.</summary>
    public IReadOnlyList<ReviewDecisionRequest> ReviewCalls { get { lock (_gate) { return [.. _reviewCalls]; } } }

    /// <summary>How many context calls were made, of any kind.</summary>
    public int ContextCalls => Volatile.Read(ref _contextCalls);

    /// <summary>The queue body as served now: the override, else the golden with the decisions applied.</summary>
    /// <returns>The answer.</returns>
    internal RouteAnswer ReviewQueueBody()
    {
        lock (_gate)
        {
            return ReviewQueueAnswer ?? new RouteAnswer(HttpStatusCode.OK, Queue().ToJsonString());
        }
    }

    /// <summary>The file map answer for a query.</summary>
    /// <param name="query">What was asked.</param>
    /// <returns>The answer.</returns>
    internal RouteAnswer FileMapFor(FileMapQuery query) => FileMapResponder?.Invoke(query) ?? FileMapAnswer ?? ContextAnswers.FileMap(query);

    /// <summary>Answers each file map read by its query (a paged folder); null falls back to <see cref="FileMapAnswer"/>.</summary>
    public Func<FileMapQuery, RouteAnswer>? FileMapResponder { get; set; }

    /// <summary>Every file map read, in order.</summary>
    public IReadOnlyList<FileMapQuery> FileMapQueries { get { lock (_gate) { return [.. _fileMapQueries]; } } }

    private readonly List<FileMapQuery> _fileMapQueries = [];

    public Task<Result<LensesView>> Lenses(Uri api, LensesQuery query, CancellationToken cancellationToken)
    {
        Log("lenses:" + string.Join(' ', query.Anchors));
        return Context<LensesView>(api, "/sindri/context/lenses", LensesAnswer(query), LensesGate, cancellationToken);
    }

    /// <summary>The code lens answer for a query.</summary>
    /// <param name="query">What was asked.</param>
    /// <returns>The answer.</returns>
    internal RouteAnswer CodeFor(CodeQuery query) => CodeAnswer ?? ContextAnswers.Code(query);

    /// <summary>Every code lens read, in order.</summary>
    public IReadOnlyList<CodeQuery> CodeQueries { get { lock (_gate) { return [.. _codeQueries]; } } }

    private readonly List<CodeQuery> _codeQueries = [];

    public Task<Result<CodeView>> Code(Uri api, CodeQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _codeQueries.Add(query);
        }

        return Context<CodeView>(api, "/sindri/context/code", CodeFor(query), CodeGate, cancellationToken);
    }

    public Task<Result<ItemScopeView>> ItemScope(Uri api, string itemId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _scopeAsked.Add(itemId);
        }

        return Context<ItemScopeView>(api, "/sindri/items/{id}/scope", ItemScopeAnswer(itemId), gate: null, cancellationToken);
    }

    // Counted apart from ContextCalls (like the publish state), which the Context page's tests hold at zero before a read of the lenses.
    public async Task<Result<WholeMapView>> WholeMap(Uri api, WholeMapQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _wholeAsked.Add(query);
        }

        await WaitFor(WholeMapGate, cancellationToken).ConfigureAwait(false);
        return NotReceived<WholeMapView>(api) ?? Answer<WholeMapView>(api, "/sindri/context/map/whole", WholeMapAnswer(query));
    }

    /// <summary>Held open until set: a whole-map read that started while it was set waits for it.</summary>
    public TaskCompletionSource? WholeMapGate { get; set; }

    public Task<Result<FileMapView>> FileMap(Uri api, FileMapQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _fileMapQueries.Add(query);
        }

        return Context<FileMapView>(api, "/sindri/context/filemap", FileMapFor(query), FileMapGate, cancellationToken);
    }

    public Task<Result<ReviewQueueView>> ReviewQueue(Uri api, int? limit, CancellationToken cancellationToken) =>
        Context<ReviewQueueView>(api, "/sindri/context/map/review", ReviewQueueBody(), ReviewQueueGate, cancellationToken);

    public async Task<Result<ReviewDecisionResponse>> ReviewDecision(Uri api, ReviewDecisionRequest request, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _contextCalls);
        if (NotReceived<ReviewDecisionResponse>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _reviewCalls.Add(request);
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        var answer = Answer<ReviewDecisionResponse>(api, "/sindri/context/map/review", ReviewDecided);
        if (answer.IsSuccess)
        {
            lock (_gate)
            {
                Apply(request, answer.Value);
            }
        }

        return answer;
    }

    public Task<Result<ReviewHistoryView>> ReviewHistory(Uri api, int? limit, CancellationToken cancellationToken) =>
        Context<ReviewHistoryView>(api, "/sindri/context/map/review/history", ReviewHistoryAnswer, ReviewHistoryGate, cancellationToken);

    public Task<Result<MapStatusView>> MapStatus(Uri api, string? anchor, CancellationToken cancellationToken) =>
        Context<MapStatusView>(api, "/sindri/context/map", MapStatusAnswer, MapStatusGate, cancellationToken);

    public Task<Result<MapPublishView>> MapPublish(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _mapPublishCalls);
        return Task.FromResult(Answer<MapPublishView>(api, "/sindri/context/map/publish", MapPublishAnswer));
    }

    public Task<Result<NodeView>> Node(Uri api, string key, CancellationToken cancellationToken)
    {
        Log("node:" + key);
        return Context<NodeView>(api, "/sindri/context/node", NodeAnswer, NodeGate, cancellationToken);
    }

    public Task<Result<SourcesView>> Sources(Uri api, CancellationToken cancellationToken) =>
        Context<SourcesView>(api, "/sindri/context/sources", SourcesAnswer, SourcesGate, cancellationToken);

    // A context read is synchronous to the context database: it never waits on the views and never uses a sequence up.
    private async Task<Result<T>> Context<T>(Uri api, string path, RouteAnswer answer, TaskCompletionSource? gate, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _contextCalls);
        await WaitFor(gate, cancellationToken).ConfigureAwait(false);
        return Answer<T>(api, path, answer);
    }

    private JsonObject Queue() => _queue ??= JsonNode.Parse(GoldenFiles.Api("context-map-review", "map-tabs-draft"))!.AsObject(); // why: the golden is a JSON object

    // A decision that settles an entry takes it off the queue under the override it wrote; an undo puts it back where it was.
    private void Apply(ReviewDecisionRequest request, ReviewDecisionResponse reply)
    {
        var written = reply.Result?.Written ?? [];
        if (request.Verb == DeskVerb.Undo)
        {
            Restore([.. written.Concat(request.Overrides ?? []).Concat(request.Override is { } one ? [one] : []).Distinct(StringComparer.Ordinal)]);
        }
        else if (_settling.Contains(request.Verb))
        {
            Settle(request, written.Count > 0 ? written[0] : request.Verb.ToString());
        }
    }

    private void Restore(IEnumerable<string> overrides)
    {
        var entries = Queue()["entries"]!.AsArray(); // why: the golden has entries
        foreach (var id in overrides)
        {
            if (_decidedEntries.Remove(id, out var back))
            {
                entries.Insert(Math.Min(back.Index, entries.Count), back.Entry);
                Shift(+1);
            }
        }
    }

    private void Settle(ReviewDecisionRequest request, string id)
    {
        var entries = Queue()["entries"]!.AsArray(); // why: the golden has entries
        var at = entries.ToList().FindIndex(e => Names(request, e!)); // why: an array element of the golden
        if (at < 0)
        {
            return;
        }

        _decidedEntries[id] = (at, entries[at]!); // why: found just above
        entries.RemoveAt(at);
        Shift(-1);
    }

    private static bool Names(ReviewDecisionRequest request, JsonNode entry) =>
        (request.Link is { } link && string.Equals((string?)entry["link"], link, StringComparison.Ordinal))
        || (request.Node is { } node && string.Equals((string?)entry["node"], node, StringComparison.Ordinal))
        || (request.Override is { } ovr && string.Equals((string?)entry["override"], ovr, StringComparison.Ordinal));

    private void Shift(int by)
    {
        var queue = Queue();
        queue["undecided"] = (int)queue["undecided"]! + by;
    }
}
