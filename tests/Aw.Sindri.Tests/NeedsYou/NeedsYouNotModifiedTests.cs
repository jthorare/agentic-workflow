using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>
/// An unchanged GET /sindri/needs-you answers 304 without reading the folded rows (#2882). Before, the tag was a hash of every
/// needs_you row joined to its pending decision, so a 304 still read and hashed the whole list (11 ms at 100k events). The tag now
/// comes from the marks of the needs_you, decisions and items views, the setup rows and the projector's state, and every one of
/// those inputs moves it.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouNotModifiedTests
{
    private const string SeedType = "test.seed";
    private static readonly DecisionId _second = DecisionId.From("DEC-00000000000000000000000000000002");
    private readonly ViewsHarness _views = new();
    private readonly Switch _switch = new() { Off = true };
    private readonly TurnOnMcpSighting _sighting = new();
    private long _sequence = 100_000;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_not_modified_answer_reads_no_needs_you_decision_or_item_rows()
    {
        await SeedDecisionRow();
        await Seed(t => t.Put(ViewNames.Items, Item(1).Id.Value, Item(1), Token));
        var first = await Query(new GetNeedsYouRequest());
        Assert.NotNull(first.Tag);
        var calls = new List<ReadCall>();

        var second = await Query(new GetNeedsYouRequest(IfNoneMatch: first.Tag), new CountingViewReader(_views.Views, calls));

        Assert.True(second.NotModified);
        Assert.Equal(first.Tag, second.Tag, StringComparer.Ordinal);
        Assert.Equal(0, calls.Sum(static c => c.Rows));   // #2882: before, every needs_you row and every pending decision was read to build the tag
    }

    [Fact]
    public async Task A_new_needs_you_row_moves_the_tag_and_brings_the_body()
    {
        await SeedDecisionRow();
        var first = await Query(new GetNeedsYouRequest());

        var other = A.NeedsYouRow().Build() with { Id = NeedsYouRowId.From("decision:" + _second.Value), Subject = new NeedsYouSubject(SubjectKind.Decision, SubjectId.From(_second.Value)) };
        await Seed(t => t.Put(ViewNames.NeedsYou, other.Id.Value, new NeedsYouRowRecord(other, _sequence), Token));
        var second = await Query(new GetNeedsYouRequest(IfNoneMatch: first.Tag));

        AssertMoved(first, second);
        Assert.Equal(2, second.Rows.Count);
    }

    [Fact]
    public async Task A_resolved_decision_moves_the_tag_and_the_row_loses_its_decision()
    {
        await SeedDecisionRow();
        var first = await Query(new GetNeedsYouRequest());
        Assert.NotNull(Assert.Single(first.Rows).Decision);

        await Seed(t => t.Put(ViewNames.Decisions, A.FirstDecisionId.Value, Decision() with { State = DecisionState.Decided, DecidedBy = "human", DecidedAt = KnownInstants.Epoch }, Token));
        var second = await Query(new GetNeedsYouRequest(IfNoneMatch: first.Tag));

        AssertMoved(first, second);
        Assert.Null(Assert.Single(second.Rows).Decision);
    }

    [Fact]
    public async Task An_item_state_change_moves_the_tag_of_an_empty_list_and_its_sentence()
    {
        await Seed(t => t.Put(ViewNames.Items, Item(1).Id.Value, Item(1), Token));
        var first = await Query(new GetNeedsYouRequest());
        Assert.Empty(first.Rows);

        await Seed(t => t.Put(ViewNames.Items, Item(1).Id.Value, Item(1) with { State = ItemState.Running }, Token));
        var second = await Query(new GetNeedsYouRequest(IfNoneMatch: first.Tag));

        AssertMoved(first, second);
        Assert.NotEqual(first.Empty, second.Empty, StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_approved_profile_change_that_adds_or_clears_a_setup_row_moves_the_tag()
    {
        await SeedDecisionRow();
        await Approve(repos: true);
        _switch.Off = false;
        var first = await Query(new GetNeedsYouRequest());
        Assert.Equal(2, first.Rows.Count);   // the decision and the "Turn on for your repos" row

        await Approve(repos: false);
        var second = await Query(new GetNeedsYouRequest(IfNoneMatch: first.Tag));

        AssertMoved(first, second);
        _ = Assert.Single(second.Rows);
        _switch.Off = true;
        await Approve(repos: true);
        Assert.False((await Query(new GetNeedsYouRequest(IfNoneMatch: first.Tag))).NotModified, "the mcp switch alone clears the row");
    }

    [Fact]
    public async Task The_clock_moving_alone_keeps_the_tag()
    {
        await SeedDecisionRow();
        await Approve(repos: true);
        _switch.Off = false;
        var first = await Query(new GetNeedsYouRequest());

        _views.Slice.Clock.Advance(TimeSpan.FromHours(3));
        var second = await Query(new GetNeedsYouRequest(IfNoneMatch: first.Tag));

        Assert.True(second.NotModified);   // no input of the answer moves with the clock: the turn-on row keeps its first sighting (#2938)
    }

    [Fact]
    public async Task A_stalled_rebuilding_or_behind_projector_moves_the_tag_with_no_row_write()
    {
        await SeedDecisionRow();
        var baseline = _views.Progress.Current;
        var first = await Query(new GetNeedsYouRequest());
        var variants = new[]
        {
            baseline with { LedgerHead = baseline.Cursor + 3 },
            baseline with { Rebuilding = true },
            baseline with { Stalled = true, StalledAt = baseline.Cursor + 1, StalledType = EventTypes.ServiceRan },
        };

        foreach (var variant in variants)
        {
            _views.Progress.Publish(variant);
            Assert.False((await Query(new GetNeedsYouRequest(IfNoneMatch: first.Tag))).NotModified, variant.ToString());
        }

        _views.Progress.Publish(baseline);
        Assert.True((await Query(new GetNeedsYouRequest(IfNoneMatch: first.Tag))).NotModified, "back to the baseline, the tag is the first one again");
    }

    private static void AssertMoved(GetNeedsYouResponse first, GetNeedsYouResponse second)
    {
        Assert.False(second.NotModified);
        Assert.NotEqual(first.Tag, second.Tag, StringComparer.Ordinal);
    }

    private static HumanDecisionRow Decision() => new(A.FirstDecisionId, A.FirstRequestId, DecisionKind.AcceptRequest, RequestKind.Rescope, Item: null, ItemVersion: 0, Repo: null,
        "Split the work into two items.", DecisionState.Pending, "session:cli", KnownInstants.Epoch, DecidedBy: null, DecidedAt: null, Choice: null, Note: null, Cleared: null, Sequence: 1);

    private static ItemRow Item(int n) => new(ItemId.From("ITM-" + n.ToString("x32", System.Globalization.CultureInfo.InvariantCulture)), "item", Repo: null, ItemState.Done, Step: null, Version: 3,
        CreatedBy: "human", KnownInstants.Epoch, Since: KnownInstants.Epoch, KnownInstants.Epoch, Link: null, Alias: null, Body: null, WaitingOn: null, Instance: null, Owner: null, Sequence: 1);

    private async Task SeedDecisionRow()
    {
        var row = A.NeedsYouRow().Build();
        await Seed(t => t.Put(ViewNames.NeedsYou, row.Id.Value, new NeedsYouRowRecord(row, _sequence), Token));
        await Seed(t => t.Put(ViewNames.Decisions, A.FirstDecisionId.Value, Decision(), Token));
    }

    /// <summary>Writes rows straight into the views, the way a fold would (one event, one projection).</summary>
    private async Task Seed(Func<IViewRows, ValueTask<Result<Unit>>> write)
    {
        var slice = _views.Slice;
        _ = (await _views.Views.Open(ViewSchema.Version, Token)).Value;
        var seed = Recorded.Of(slice.Events, SeedType, new ServiceRan(slice.Ids.New<CommandId>(), ServiceName.From("heartbeat"), slice.Clock.GetUtcNow(), ServiceOutcome.Ok, Code: null), ++_sequence);
        _ = (await _views.Views.Apply([seed], [new SeedRows(write)], slice.Clock.GetUtcNow(), Token)).Value;
    }

    /// <summary>Saves an approved profile that names a repo, or none.</summary>
    private async Task Approve(bool repos)
    {
        var live = SliceHarness.Profile();
        var profile = live.Profile with { Repos = repos ? [new RepoEntry("demo", "/work/demo")] : [] };
        var loaded = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), live.Source);
        _ = await _views.Slice.Profiles.Save(loaded, Token);
    }

    private async Task<GetNeedsYouResponse> Query(GetNeedsYouRequest request, IViewReader? reader = null)
    {
        var slice = _views.Slice;
        if (slice.Held.Current is null)
        {
            _ = await slice.HoldLease();
        }

        var jobs = new InMemoryJobViews();
        var gate = new McpAutoGate(slice.Profiles, new McpEvents(slice.Held, slice.Store, slice.Events, slice.Callers), _switch, jobs);
        var live = new NeedsYouLiveRows(new SetupNotices(jobs, slice.Profiles, new PassScrubber(), slice.Clock, gate, _sighting));
        using var entered = slice.Callers.Enter(A.Caller().Human().Build());
        var result = await new GetNeedsYouService(reader ?? _views.Views, _views.Progress, slice.Callers, live, SliceHarness.Json).Handle(request, Token);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        return result.Value;
    }

    private sealed record ReadCall(string View, string Method, int Rows);

    private sealed class SeedRows(Func<IViewRows, ValueTask<Result<Unit>>> write) : IProjection
    {
        public IReadOnlyList<string> Views => [ViewNames.NeedsYou, ViewNames.Decisions, ViewNames.Items];

        public bool Handles(string eventType) => eventType == SeedType;

        public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows target, CancellationToken cancellationToken) => await write(target);
    }

    private sealed class Switch : IMcpAutoSwitch
    {
        public bool Off { get; set; }
    }

    private sealed class PassScrubber : ITextScrubber
    {
        public string Scrub(string text) => text;
    }

    /// <summary>The real reader, counting every call and the rows it returns.</summary>
    private sealed class CountingViewReader(IViewReader inner, List<ReadCall> calls) : IViewReader
    {
        public async ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
            where TRow : class
        {
            var result = await inner.Get<TRow>(view, key, cancellationToken);
            calls.Add(new ReadCall(view, "Get", result.IsSuccess && result.Value is not null ? 1 : 0));
            return result;
        }

        public async ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
            where TRow : class => Count(view, "List", await inner.List<TRow>(view, cancellationToken));

        public async ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
            where TRow : class => Count(view, "Range", await inner.Range<TRow>(view, afterKey, beforeKey, max, cancellationToken));

        public async ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
            where TRow : class => Count(view, "Where", await inner.Where<TRow>(view, column, value, cancellationToken));

        public ValueTask<Result<string>> Mark(string view, CancellationToken cancellationToken) => inner.Mark(view, cancellationToken);

        public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) => inner.State(cancellationToken);

        private Result<IReadOnlyList<TRow>> Count<TRow>(string view, string method, Result<IReadOnlyList<TRow>> result)
        {
            calls.Add(new ReadCall(view, method, result.IsSuccess ? result.Value.Count : 0));
            return result;
        }
    }
}
