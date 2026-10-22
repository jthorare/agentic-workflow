using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The P3 routes (spec-shaped until P3's goldens land): the queue, an item, its timeline, CreateItem and CloseItem.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<CreateItemRequest> _created = [];
    private readonly List<(string Item, CloseItemRequest Request)> _closed = [];
    private int _itemCalls;
    private int _timelineCalls;

    /// <summary>GET /sindri/queue's answer.</summary>
    public RouteAnswer QueueView { get; set; } = RouteAnswer.SpecShaped("queue", "fixture");

    /// <summary>GET /sindri/items/{id}'s answer per id; an id not here answers SND-ITEM-404.</summary>
    public Dictionary<string, RouteAnswer> Items { get; } = new(StringComparer.Ordinal)
    {
        [A.NewItem] = RouteAnswer.SpecShaped("items-id", "new"),
        [A.DecisionItem] = RouteAnswer.SpecShaped("items-id", "extra-p4-needs-decision"),
    };

    /// <summary>GET /sindri/items/{id}/timeline's page per 'after' (any item without its own page in <see cref="ItemTimelinePages"/>).</summary>
    public Dictionary<long, RouteAnswer> TimelinePages { get; } = new()
    {
        [0] = RouteAnswer.SpecShaped("items-id-timeline", "fixture"),
        [151] = RouteAnswer.SpecShaped("items-id-timeline", "page-2"),
    };

    /// <summary>Held open until set: the queue read waits for it (the skeleton shows until the first answer).</summary>
    public TaskCompletionSource? QueueGate { get; set; }

    /// <summary>Held open until set, per item id: that item's read waits for its gate (others answer at once).</summary>
    public Dictionary<string, TaskCompletionSource> ItemGates { get; } = new(StringComparer.Ordinal);

    /// <summary>GET /sindri/items/{id}/timeline's page for one item and 'after' (before <see cref="TimelinePages"/>).</summary>
    public Dictionary<(string Item, long After), RouteAnswer> ItemTimelinePages { get; } = [];

    /// <summary>Held open until set, per item and 'after' cursor: that timeline page's read waits for its gate.</summary>
    public Dictionary<(string Item, long After), TaskCompletionSource> TimelineGates { get; } = [];

    /// <summary>
    /// When true a gated read answers once its gate opens even if its caller was cancelled meanwhile, as a response already on
    /// the wire does: the view model's own generation checks, not the cancellation, must then drop a late answer.
    /// </summary>
    public bool IgnoreCancellation { get; set; }

    /// <summary>POST /sindri/items' answer.</summary>
    public RouteAnswer Create { get; set; } = RouteAnswer.SpecShaped("items", "created");

    /// <summary>POST /sindri/items/{id}/close's answer.</summary>
    public RouteAnswer Close { get; set; } = RouteAnswer.SpecShaped("items-id-close", "accepted");

    /// <summary>Held open until set, to see a command's sending phase.</summary>
    public TaskCompletionSource? CommandGate { get; set; }

    /// <summary>Runs after an accepted CloseItem, as the projector would move the item (a test sets the next answers here).</summary>
    public Action<string, CloseItemRequest>? OnClosed { get; set; }

    /// <summary>Every CreateItem body received, in order.</summary>
    public IReadOnlyList<CreateItemRequest> Created { get { lock (_gate) { return [.. _created]; } } }

    /// <summary>Every CloseItem received (item, body), in order.</summary>
    public IReadOnlyList<(string Item, CloseItemRequest Request)> Closed { get { lock (_gate) { return [.. _closed]; } } }

    /// <summary>How many times an item was read.</summary>
    public int ItemCalls => Volatile.Read(ref _itemCalls);

    /// <summary>How many timeline pages were read.</summary>
    public int TimelineCalls => Volatile.Read(ref _timelineCalls);

    /// <summary>The answer for one item id (the handler serves the same text).</summary>
    /// <param name="itemId">The id.</param>
    /// <returns>Its answer, or the SND-ITEM-404 golden.</returns>
    public RouteAnswer ItemAnswer(string itemId) =>
        Items.TryGetValue(itemId, out var answer) ? answer : RouteAnswer.Problem("items-id", "not-found");

    /// <summary>The page for one cursor; a cursor with no page answers an empty last page.</summary>
    /// <param name="itemId">The item.</param>
    /// <param name="after">The cursor.</param>
    /// <returns>The page's answer.</returns>
    public RouteAnswer TimelineAnswer(string itemId, long after) =>
        ItemTimelinePages.TryGetValue((itemId, after), out var own) ? own
        : TimelinePages.TryGetValue(after, out var page) ? page
            : new RouteAnswer(System.Net.HttpStatusCode.OK, """{"entries":[],"next":null,"asOf":{"cursor":57,"ledgerHead":57,"projectedAt":null,"rebuilding":false,"stalled":false}}""");

    public Task<Result<QueueView>> Queue(Uri api, CancellationToken cancellationToken) => Queue(api, sequence: null, cancellationToken);

    /// <summary>A queue read that carries <paramref name="sequence"/> as X-Sindri-After (see <see cref="Status(Uri, long?, CancellationToken)"/>).</summary>
    /// <param name="api">The API.</param>
    /// <param name="sequence">The sequence the request carried, or null for a direct call.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The answer.</returns>
    internal Task<Result<QueueView>> Queue(Uri api, long? sequence, CancellationToken cancellationToken) =>
        View<QueueView>(api, "/sindri/queue", QueueView, sequence, QueueGate, cancellationToken);

    /// <summary>The body GET /sindri/items?state= answers, by state; null serves an empty list.</summary>
    public Func<string, RouteAnswer?>? ListItemsBody { get; set; }

    /// <summary>The states ListItems was asked for, in order.</summary>
    public IReadOnlyList<string> ListedStates { get { lock (_gate) { return [.. _listed]; } } }

    private readonly List<string> _listed = [];

    public Task<Result<ItemListView>> ListItems(Uri api, string state, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _listed.Add(state);
        }

        var answer = ListItemsBody?.Invoke(state) ?? new RouteAnswer(System.Net.HttpStatusCode.OK, """{"items":[],"empty":null,"asOf":{"cursor":1,"ledgerHead":1,"projectedAt":null,"rebuilding":false,"stalled":false}}""");
        return View<ItemListView>(api, "/sindri/items", answer, sequence: null, gate: null, cancellationToken);
    }

    public Task<Result<ItemView>> Item(Uri api, string itemId, CancellationToken cancellationToken) => Item(api, itemId, sequence: null, cancellationToken);

    /// <summary>An item read that carries <paramref name="sequence"/> as X-Sindri-After.</summary>
    /// <param name="api">The API.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="sequence">The sequence the request carried, or null for a direct call.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The answer.</returns>
    internal Task<Result<ItemView>> Item(Uri api, string itemId, long? sequence, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _itemCalls);
        return View<ItemView>(api, "/sindri/items/{id}", ItemAnswer(itemId), sequence, ItemGates.GetValueOrDefault(itemId), cancellationToken);
    }

    public Task<Result<TimelineView>> Timeline(Uri api, string itemId, long after, CancellationToken cancellationToken) =>
        Timeline(api, itemId, after, sequence: null, cancellationToken);

    /// <summary>A timeline read that carries <paramref name="sequence"/> as X-Sindri-After.</summary>
    /// <param name="api">The API.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="after">The timeline cursor.</param>
    /// <param name="sequence">The sequence the request carried, or null for a direct call.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The answer.</returns>
    internal Task<Result<TimelineView>> Timeline(Uri api, string itemId, long after, long? sequence, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _timelineCalls);
        return View<TimelineView>(api, "/sindri/items/{id}/timeline", TimelineAnswer(itemId, after), sequence, TimelineGates.GetValueOrDefault((itemId, after)), cancellationToken);
    }

    public async Task<Result<ItemCreatedView>> CreateItem(Uri api, CreateItemRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<ItemCreatedView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _created.Add(request);
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        var created = Answer<ItemCreatedView>(api, "/sindri/items", Create);
        if (created.IsSuccess)
        {
            _ = Interlocked.Exchange(ref _pending, created.Value.Accepted.Sequence);
        }

        return created;
    }

    public async Task<Result<CommandAcceptedView>> CloseItem(Uri api, string itemId, CloseItemRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<CommandAcceptedView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _closed.Add((itemId, request));
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        var answer = Answer<CommandAcceptedView>(api, "/sindri/items/{id}/close", Close);
        if (answer.IsSuccess)
        {
            _ = Interlocked.Exchange(ref _pending, answer.Value.Sequence);
            OnClosed?.Invoke(itemId, request);
        }

        return answer;
    }

    // The server's order, as for status: nothing answers, then the caller check; a command that never got there is not recorded.
    private Result<T>? NotReceived<T>(Uri api)
    {
        if (Down)
        {
            return Unreachable<T>(api);
        }

        return Refusal is { } refusal ? Refused<T>(refusal) : null;
    }

    // A view read: the sequence is used once, whatever the answer is; the views lag only for a read that carried one.
    private async Task<Result<T>> View<T>(Uri api, string path, RouteAnswer answer, long? sequence, TaskCompletionSource? gate, CancellationToken cancellationToken)
    {
        var carried = Carried(sequence, unreachable: Down);
        await WaitFor(gate, cancellationToken).ConfigureAwait(false);
        return !Down && Refusal is null && carried > 0 && Lag != ViewsLag.None ? Behind(carried) : Answer<T>(api, path, answer);
    }

    private async Task WaitFor(TaskCompletionSource? gate, CancellationToken cancellationToken)
    {
        if (gate is not null)
        {
            await gate.Task.WaitAsync(IgnoreCancellation ? CancellationToken.None : cancellationToken).ConfigureAwait(false);
        }
    }
}
