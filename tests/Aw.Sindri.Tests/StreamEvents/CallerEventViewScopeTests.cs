using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.StreamEvents;

/// <summary>What a session's read of the ledger scans past and what it does when an item's repo can't be read (API spec §3.1, §5.2).</summary>
public sealed class CallerEventViewScopeTests
{
    private static readonly ItemId _other = ItemId.From("ITM-00000000000000000000000000000002");
    private static readonly LedgerFilter _all = new(Types: null, Item: null);
    private readonly SliceHarness _h = new();

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_page_scans_on_past_rows_of_another_repo_until_it_has_a_full_page()
    {
        var grant = await _h.HoldLease();
        await AppendItem(grant, _other, "elsewhere");
        await AppendItem(grant, A.FirstItemId, "demo");
        using var entered = _h.Callers.Enter(Session());

        var page = (await View(StreamEventsServices.Repos(_h)).Read(new EventQuery(After: 0, Max: 2, _all, IncludeNonPublic: false), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([3L, 4L], page.Events.Select(e => e.Sequence));   // the first scan of two rows held only the other repo's
        Assert.Equal(4L, page.ScannedTo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_that_cannot_be_read_keeps_the_rows_before_it_and_leaves_the_cursor_before_the_failed_row()
    {
        await AppendItem(await _h.HoldLease(), A.FirstItemId, "demo");
        using var entered = _h.Callers.Enter(Session());

        var page = (await View(new FailingItemRepos(StreamEventsServices.Repos(_h), failAt: 2)).Read(new EventQuery(After: 0, Max: 10, _all, IncludeNonPublic: false), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([1L], page.Events.Select(e => e.Sequence));
        Assert.Equal(1L, page.ScannedTo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_that_cannot_be_read_with_nothing_kept_is_the_error()
    {
        await AppendItem(await _h.HoldLease(), A.FirstItemId, "demo");
        using var entered = _h.Callers.Enter(Session());

        var page = await View(new FailingItemRepos(StreamEventsServices.Repos(_h), failAt: 1)).Read(new EventQuery(After: 0, Max: 10, _all, IncludeNonPublic: false), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.LedgerUnavailable.Code, page.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_item_less_request_and_its_decision_reach_only_the_sessions_of_their_repo()
    {
        var grant = await _h.HoldLease();
        await AppendRequestAndDecision(grant, RepoName.From("elsewhere"));
        using var entered = _h.Callers.Enter(Session());

        var page = (await View(StreamEventsServices.Repos(_h)).Read(new EventQuery(After: 0, Max: 10, _all, IncludeNonPublic: false), TestContext.Current.CancellationToken)).Value;

        Assert.Empty(page.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_of_the_repo_sees_the_decision_and_its_choice_but_not_the_note()
    {
        var grant = await _h.HoldLease();
        await AppendRequestAndDecision(grant, RepoName.From("demo"));
        using var entered = _h.Callers.Enter(Session());

        var page = (await View(StreamEventsServices.Repos(_h)).Read(new EventQuery(After: 0, Max: 10, _all, IncludeNonPublic: false), TestContext.Current.CancellationToken)).Value;

        Expect.Equal($"{EventTypes.RequestFiled} {EventTypes.DecisionRecorded}", string.Join(' ', page.Events.Select(e => e.Type)));
        var decided = page.Events[1].Payload;
        Assert.Equal(("accept", "demo", false), (decided.GetProperty("choice").GetString(), decided.GetProperty("repo").GetString(), decided.TryGetProperty("note", out _)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_request_with_no_repo_and_its_decision_are_seen_by_every_session()
    {
        var grant = await _h.HoldLease();
        await AppendRequestAndDecision(grant, repo: null);
        using var entered = _h.Callers.Enter(Session());

        var page = (await View(StreamEventsServices.Repos(_h)).Read(new EventQuery(After: 0, Max: 10, _all, IncludeNonPublic: false), TestContext.Current.CancellationToken)).Value;

        Expect.Equal($"{EventTypes.RequestFiled} {EventTypes.DecisionRecorded}", string.Join(' ', page.Events.Select(e => e.Type)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_humans_ledger_read_keeps_the_note_whole()
    {
        var grant = await _h.HoldLease();
        await AppendRequestAndDecision(grant, RepoName.From("elsewhere"));
        using var entered = _h.Callers.Enter(new CallerContext(CallerClass.Human, Terminal: true, "human:cli", _h.Ids.New<CommandId>(), _h.Ids.New<CorrelationId>(), Repo: null));

        var page = (await View(StreamEventsServices.Repos(_h)).Read(new EventQuery(After: 0, Max: 10, _all, IncludeNonPublic: true), TestContext.Current.CancellationToken)).Value;

        Assert.Equal("go ahead", page.Events.Single(e => e.Type == EventTypes.DecisionRecorded).Payload.GetProperty("note").GetString());
    }

    private async Task AppendRequestAndDecision(Core.Lease.LeaseGrant grant, RepoName? repo)
    {
        var ct = TestContext.Current.CancellationToken;
        var filed = new RequestFiled(_h.Ids.New<CommandId>(), A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, Item: null, repo, "split it");
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.RequestFiled, filed, $"request.filed:{A.FirstRequestId.Value}"), grant, ct);
        var decided = new DecisionRecorded(_h.Ids.New<CommandId>(), A.FirstDecisionId, DecisionOption.From("accept"), "go ahead", "human:ui", repo);
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.DecisionRecorded, decided, $"decision.recorded:{A.FirstDecisionId.Value}"), grant, ct);
    }

    private CallerEventView View(IItemRepos repos) => new(_h.Callers, repos, new PassThroughScrubber(), SliceHarness.Json, _h.Store);

    private CallerContext Session() => new(CallerClass.Session, Terminal: false, "session:cli", _h.Ids.New<CommandId>(), _h.Ids.New<CorrelationId>(), RepoName.From("demo"));

    /// <summary>The item's creation and one state change: two rows about it.</summary>
    private async Task AppendItem(Core.Lease.LeaseGrant grant, ItemId item, string repo)
    {
        var ct = TestContext.Current.CancellationToken;
        var created = new ItemCreated(_h.Ids.New<CommandId>(), item, "t", RepoName.From(repo), Link: null, Body: null, Owner: null);
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.ItemCreated, created, "item.created:" + item.Value, item.Value), grant, ct);
        var moved = new ItemStateChanged(_h.Ids.New<CommandId>(), item, ItemState.New, ItemState.Queued, 2, Reason: null);
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.ItemStateChanged, moved, "item.state-changed:" + item.Value + ":v2", item.Value), grant, ct);
    }
}
