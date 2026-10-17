using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IViewStore"/> (with its <see cref="IViewReader"/>) honours (API spec §2.3): a page
/// folds each event through the projections that handle it, a failing event stops the page there and keeps the events
/// before it, a reset empties the views and the cursor, and rows round-trip through JSON.
/// </summary>
public abstract class ViewStoreContract
{
    private const string Put = "fixture.put";
    private const string Fail = "fixture.fail";
    private const string Throw = "fixture.throw";
    private const string PutRows = "fixture.rows";
    private const string HitRepo = "hit-repo";
    private static readonly ItemId _hitItem = ItemId.From("ITM-00000000000000000000000000000002");
    private static readonly RequestId _hitRequest = RequestId.From("REQ-00000000000000000000000000000002");
    private static readonly DecisionId _hitDecision = DecisionId.From("DEC-00000000000000000000000000000002");
    private static readonly NeedsYouRowId _hitNeedsYou = NeedsYouRowId.From("failure:service:hit");
    private readonly SequentialIdGenerator _ids = new();

    /// <summary>The contract's clock.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A store and its reader over a new, empty file or memory.</summary>
    /// <returns>The store and the reader.</returns>
    protected abstract (IViewStore Store, IViewReader Reader) Create();

    /// <summary>The first Open creates the store (Fresh, cursor 0); the next one finds it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Open_on_a_new_file_is_fresh()
    {
        var (store, reader) = Create();
        Expect.True((await reader.State(Token)).Value is null);

        var first = (await store.Open(ViewSchema.Version, Token)).Value;
        var second = (await store.Open(ViewSchema.Version, Token)).Value;

        Expect.Equal(new ProjectorState(0, ViewSchema.Version, ProjectedAt: null, Rebuilding: false, StalledAt: null, StalledType: null, Fresh: true), first);
        Expect.True(!second.Fresh);
        Expect.Equal(0L, (await reader.State(Token)).Value?.Cursor);
    }

    /// <summary>Each event goes through every projection that handles it; the cursor and ProjectedAt move to the page's end.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Apply_folds_each_event_through_the_projections_that_handle_it()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);
        var at = Clock.GetUtcNow();

        var applied = (await store.Apply([Event(1, Put, "alpha"), Event(2, "other", "x"), Event(3, Put, "beta")], Projections(), at, Token)).Value;

        Expect.True(applied.Stall is null);
        Expect.Equal(3L, applied.State.Cursor);
        Expect.Equal(at, applied.State.ProjectedAt);
        Expect.Sequence(["alpha", "beta"], (await reader.List<ServiceRow>(ViewNames.Services, Token)).Value.Select(r => r.Name.Value));
        Expect.Sequence(["alpha", "beta"], (await reader.List<ServiceRow>(ViewNames.Profile, Token)).Value.Select(r => r.Name.Value));
        Expect.Equal(3L, (await reader.State(Token)).Value?.Cursor);
    }

    /// <summary>A failing event stops the page: the events before it stay, its own writes and the later events don't, and the stall is recorded.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failing_event_stops_the_page_there_and_keeps_the_events_before_it()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);

        var applied = (await store.Apply([Event(1, Put, "alpha"), Event(2, Fail, "bad"), Event(3, Put, "beta")], Projections(), Clock.GetUtcNow(), Token)).Value;

        Expect.Equal(new ViewStall(2, Fail, FixtureProjection.Failure.Code, FixtureProjection.Failure.Message), applied.Stall);
        var state = (await reader.State(Token)).Value;
        Expect.Equal(1L, state?.Cursor);
        Expect.Equal(2L, state?.StalledAt);
        Expect.Equal(Fail, state?.StalledType);
        Expect.Sequence(["alpha"], (await reader.List<ServiceRow>(ViewNames.Services, Token)).Value.Select(r => r.Name.Value));
    }

    /// <summary>A reset empties the named views, sets the cursor to 0, clears the stall and marks rebuilding until FinishRebuild.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Reset_empties_the_views_and_the_cursor()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);
        _ = await store.Apply([Event(1, Put, "alpha"), Event(2, Fail, "bad")], Projections(), Clock.GetUtcNow(), Token);

        var reset = (await store.Reset(ViewNames.All, ViewSchema.Version + 1, Clock.GetUtcNow(), Token)).Value;

        Expect.True(reset is { Cursor: 0, Rebuilding: true, StalledAt: null, StalledType: null, SchemaVersion: ViewSchema.Version + 1 });
        Expect.Equal(0, (await reader.List<ServiceRow>(ViewNames.Services, Token)).Value.Count);
        Expect.Equal(0, (await reader.List<ServiceRow>(ViewNames.Profile, Token)).Value.Count);
        _ = (await store.FinishRebuild(Token)).Value;
        Expect.True((await reader.State(Token)).Value is { Rebuilding: false, Cursor: 0 });
    }

    /// <summary>A row reads back equal to what the projection wrote, nested records, enums and times included.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Rows_round_trip_through_json()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);
        var recorded = Event(1, Put, "alpha");

        _ = await store.Apply([recorded], Projections(), Clock.GetUtcNow(), Token);

        Expect.Equal(FixtureProjection.RowFor(recorded), (await reader.Get<ServiceRow>(ViewNames.Services, "alpha", Token)).Value);
        Expect.True((await reader.Get<ServiceRow>(ViewNames.Services, "missing", Token)).Value is null);
    }

    /// <summary>A projection that throws stalls the page like one that returns an error (API spec §2.3).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_throwing_projection_stalls_like_a_failing_one()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);

        var applied = (await store.Apply([Event(1, Put, "alpha"), Event(2, Throw, "boom")], [.. Projections(), new Throwing()], Clock.GetUtcNow(), Token)).Value;

        Expect.Equal(2L, applied.Stall?.Sequence);
        Expect.Equal(SindriErrors.ViewsStalled.Code, applied.Stall?.Code);
        Expect.True((await reader.State(Token)).Value is { Cursor: 1, StalledAt: 2 });
    }

    /// <summary>A range reads the keys strictly between its bounds, in key order, and stops at max (the item timeline's pages).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Range_returns_keys_between_the_bounds_in_order()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);
        _ = await store.Apply([Event(1, Put, "k-03"), Event(2, Put, "k-01"), Event(3, Put, "k-04"), Event(4, Put, "k-02"), Event(5, Put, "l-01")],
            Projections(), Clock.GetUtcNow(), Token);

        async Task<IEnumerable<string>> Keys(string after, string? before, int max) =>
            (await reader.Range<ServiceRow>(ViewNames.Services, after, before, max, Token)).Value.Select(r => r.Name.Value);

        Expect.Sequence(["k-02", "k-03"], await Keys("k-01", "k-04", max: 10));
        Expect.Sequence(["k-01", "k-02"], await Keys("k-", "k-~", max: 2));
        Expect.Sequence(["k-04", "l-01"], await Keys("k-03", before: null, max: 10));
        Expect.True(!(await Keys("l-01", before: null, max: 10)).Any());
    }

    /// <summary>The decisions view is filtered by its indexed columns (state, item, repo), in key order, and a column the view doesn't index is refused.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Decisions_view_filters_by_pending_and_item()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);
        var other = ItemId.From("ITM-00000000000000000000000000000002");
        var rows = new Dictionary<string, HumanDecisionRow>(StringComparer.Ordinal)
        {
            ["d1"] = A.Decision().Build(),
            ["d2"] = A.Decision().WithIds(DecisionId.From("DEC-00000000000000000000000000000002"), A.FirstRequestId).About(A.FirstItemId, 2, RepoName.From("agentic-workflow")).Build(),
            ["d3"] = A.Decision().WithIds(DecisionId.From("DEC-00000000000000000000000000000003"), A.FirstRequestId).About(other, 1, RepoName.From("agentic-workflow"))
                .Decided(DecisionOption.From("accept"), "human:ui", Clock.GetUtcNow(), "decision accepted by human:ui").Build(),
        };

        _ = await store.Apply([Event(1, "fixture.decision", "d3"), Event(2, "fixture.decision", "d1"), Event(3, "fixture.decision", "d2")],
            [new PutDecisions(rows)], Clock.GetUtcNow(), Token);

        async Task<IEnumerable<string>> Ids(ViewColumn column, string value) =>
            (await reader.Where<HumanDecisionRow>(ViewNames.Decisions, column, value, Token)).Value.Select(r => r.Id.Value[^1..]);

        Expect.Sequence(["1", "2"], await Ids(ViewColumn.State, "pending"));
        Expect.Sequence(["3"], await Ids(ViewColumn.State, "decided"));
        Expect.Sequence(["2"], await Ids(ViewColumn.Item, A.FirstItemId.Value));
        Expect.Sequence(["2", "3"], await Ids(ViewColumn.Repo, "agentic-workflow"));
        Expect.True(!(await Ids(ViewColumn.State, "voided")).Any());
        Expect.Equal(SindriErrors.InvalidInput.Code, (await reader.Where<HumanDecisionRow>(ViewNames.Decisions, ViewColumn.Kind, "x", Token)).Error.Code);
        Expect.Equal(SindriErrors.InvalidInput.Code, (await reader.Where<ServiceRow>(ViewNames.Services, ViewColumn.State, "x", Token)).Error.Code);
    }

    /// <summary>The same filter inside the page a projection folds sees the page's own earlier writes (the voiding of an item's decisions).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Where_inside_a_page_sees_the_pages_earlier_writes()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);
        var rows = new Dictionary<string, HumanDecisionRow>(StringComparer.Ordinal) { ["d1"] = A.Decision().About(A.FirstItemId, 1, repo: null).Build() };
        var seen = new List<int>();

        _ = await store.Apply([Event(1, "fixture.decision", "d1"), Event(2, "fixture.count", "item")], [new PutDecisions(rows), new CountForItem(seen)], Clock.GetUtcNow(), Token);

        Expect.Sequence([1], seen);
        Expect.Equal(1, (await reader.Where<HumanDecisionRow>(ViewNames.Decisions, ViewColumn.Item, A.FirstItemId.Value, Token)).Value.Count);
    }

    /// <summary>Every (view, column) pair, for the filter theory.</summary>
    /// <returns>The pairs.</returns>
    public static TheoryData<string, ViewColumn> EveryViewAndColumn()
    {
        var pairs = new TheoryData<string, ViewColumn>();
        foreach (var view in ViewNames.All.Where(static v => !string.Equals(v, ViewNames.Jobs, StringComparison.Ordinal) && !string.Equals(v, ViewNames.Launches, StringComparison.Ordinal)))   // the jobs and launches views' columns are pinned by SqliteJobViewsTests and SqliteLaunchViewsTests
        {
            foreach (var column in Enum.GetValues<ViewColumn>())
            {
                pairs.Add(view, column);
            }
        }

        return pairs;
    }

    /// <summary>
    /// A column <see cref="ViewColumns"/> maps for a view finds exactly the one row of that view whose value matches (real rows, so
    /// the store's own index on the JSON path is what answers); a column it doesn't map, and every column of a view that indexes
    /// none, is refused. The in-memory fake and SQLite's generated columns both follow that one map.
    /// </summary>
    /// <param name="view">A view.</param>
    /// <param name="column">A column.</param>
    /// <returns>The test.</returns>
    [Theory]
    [MemberData(nameof(EveryViewAndColumn))]
    public async Task Where_follows_the_column_map_for_every_view_and_column(string view, ViewColumn column)
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);
        _ = await store.Apply([Event(1, PutRows, "all")], [new PutRealRows()], Clock.GetUtcNow(), Token);

        if (ViewColumns.JsonPath(view, column) is null)
        {
            Expect.Equal(SindriErrors.InvalidInput.Code, (await reader.Where<ServiceRow>(view, column, "x", Token)).Error.Code);
            return;
        }

        var value = column switch
        {
            ViewColumn.Item => _hitItem.Value,
            ViewColumn.Repo => HitRepo,
            ViewColumn.Kind => "failure",
            ViewColumn.State => view == ViewNames.Requests ? "accepted" : "decided",
            _ => throw new ArgumentOutOfRangeException(nameof(column), column, "not a view column"),
        };
        var found = view switch
        {
            ViewNames.Requests => (await reader.Where<RequestRow>(view, column, value, Token)).Value.Select(r => r.Id.Value),
            ViewNames.Decisions => (await reader.Where<HumanDecisionRow>(view, column, value, Token)).Value.Select(r => r.Id.Value),
            ViewNames.NeedsYou => (await reader.Where<NeedsYouRowRecord>(view, column, value, Token)).Value.Select(r => r.Row.Id.Value),
            _ => throw new InvalidOperationException($"{view} indexes no column"),
        };
        Expect.Sequence([view switch { ViewNames.Requests => _hitRequest.Value, ViewNames.Decisions => _hitDecision.Value, _ => _hitNeedsYou.Value }], found);
    }

    private static IReadOnlyList<IProjection> Projections() =>
        [new FixtureProjection(ViewNames.Services, Put), new FixtureProjection(ViewNames.Profile, Put), new FixtureProjection(ViewNames.Services, Fail, fails: true)];

    private LedgerEvent Event(long sequence, string type, string key)
    {
        var context = EventContext.Create("host:sindri", "local", 0, _ids.NewId()).Value;
        Clock.Advance(TimeSpan.FromMilliseconds(1));
        return new LedgerEvent(sequence, 1, context.Envelope(type, """{"n":1}""", Clock, _ids), key, Item: null);
    }

    /// <summary>Puts a row that matches every filter value and one that matches none into each view that indexes columns.</summary>
    private sealed class PutRealRows : IProjection
    {
        public IReadOnlyList<string> Views => [ViewNames.Requests, ViewNames.Decisions, ViewNames.NeedsYou];

        public bool Handles(string eventType) => string.Equals(eventType, PutRows, StringComparison.Ordinal);

        public async ValueTask<Kernel.Result<Kernel.Unit>> Apply(LedgerEvent recorded, IViewRows target, CancellationToken cancellationToken)
        {
            var repo = RepoName.From(HitRepo);
            var rows = new (string View, string Key, object Row)[]
            {
                (ViewNames.Requests, _hitRequest.Value, A.Request().WithIds(_hitRequest, _hitDecision).About(_hitItem, repo).InState(RequestState.Accepted).Build()),
                (ViewNames.Requests, A.FirstRequestId.Value, A.Request().About(A.FirstItemId, RepoName.From("miss-repo")).Build()),
                (ViewNames.Decisions, _hitDecision.Value, A.Decision().WithIds(_hitDecision, _hitRequest).About(_hitItem, 1, repo)
                    .Decided(DecisionOption.From("accept"), "human:ui", KnownInstants.Epoch, "decision accepted by human:ui").Build()),
                (ViewNames.Decisions, A.FirstDecisionId.Value, A.Decision().About(A.FirstItemId, 1, RepoName.From("miss-repo")).Build()),
                (ViewNames.NeedsYou, _hitNeedsYou.Value, new NeedsYouRowRecord(A.NeedsYouRow().InRepo(repo).Build() with { Id = _hitNeedsYou, Kind = NeedsYouKind.Failure, Item = _hitItem }, 1)),
                (ViewNames.NeedsYou, "decision:miss", new NeedsYouRowRecord(A.NeedsYouRow().InRepo(RepoName.From("miss-repo")).Build() with { Item = A.FirstItemId }, 1)),
            };
            foreach (var (view, key, row) in rows)
            {
                var put = row switch
                {
                    RequestRow r => await target.Put(view, key, r, cancellationToken).ConfigureAwait(false),
                    HumanDecisionRow d => await target.Put(view, key, d, cancellationToken).ConfigureAwait(false),
                    NeedsYouRowRecord n => await target.Put(view, key, n, cancellationToken).ConfigureAwait(false),
                    _ => throw new InvalidOperationException("not a row of these views"),
                };
                if (put.IsError)
                {
                    return put.Error;
                }
            }

            return Kernel.Unit.Value;
        }
    }

    private sealed class PutDecisions(Dictionary<string, HumanDecisionRow> rows) : IProjection
    {
        public IReadOnlyList<string> Views => [ViewNames.Decisions];

        public bool Handles(string eventType) => string.Equals(eventType, "fixture.decision", StringComparison.Ordinal);

        public ValueTask<Kernel.Result<Kernel.Unit>> Apply(LedgerEvent recorded, IViewRows target, CancellationToken cancellationToken) =>
            target.Put(ViewNames.Decisions, rows[recorded.IdempotencyKey].Id.Value, rows[recorded.IdempotencyKey], cancellationToken);
    }

    private sealed class CountForItem(List<int> seen) : IProjection
    {
        public IReadOnlyList<string> Views => [];

        public bool Handles(string eventType) => string.Equals(eventType, "fixture.count", StringComparison.Ordinal);

        public async ValueTask<Kernel.Result<Kernel.Unit>> Apply(LedgerEvent recorded, IViewRows target, CancellationToken cancellationToken)
        {
            var found = await target.Where<HumanDecisionRow>(ViewNames.Decisions, ViewColumn.Item, A.FirstItemId.Value, cancellationToken).ConfigureAwait(false);
            seen.Add(found.Value.Count);
            return Kernel.Unit.Value;
        }
    }

    private sealed class Throwing : IProjection
    {
        public IReadOnlyList<string> Views => [ViewNames.Services];

        public bool Handles(string eventType) => string.Equals(eventType, Throw, StringComparison.Ordinal);

        public ValueTask<Kernel.Result<Kernel.Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("fixture: the fold threw");
    }
}
