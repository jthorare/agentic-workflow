using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.HomeCollect;
using Aw.Sindri.Features.HomeRuns;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.HomeRuns;

/// <summary>
/// The 'home-runs' collector (home spec §3.2 'run-failed', E1): a failed live run the owner has not seen raises one 'home.row.raised' of kind run-failed;
/// a seen run, or one whose row the owner marked done or muted, clears it; a quiet pass writes nothing.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class HomeRunsServiceTests
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private readonly SliceHarness _h = new();
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly InMemoryHomeView _view = new();
    private readonly HomeRunsService _service;
    private LeaseGrant? _grant;
    private long _projected;

    public HomeRunsServiceTests()
    {
        _runs = new InMemoryWorkflowRunStore(_h.Clock);
        _service = new HomeRunsService(_runs, _view, new HomeRowWriter(_h.Store, _h.Events), new PassThroughScrubber(), _h.Clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_failed_run_raises_one_run_failed_row_and_the_next_pass_writes_nothing()
    {
        var failed = await Fail(1, "step 'fetch' failed: 502");

        var first = await Tick();
        var second = await Tick();

        var raised = Assert.Single(await Events());
        Assert.Equal(EventTypes.HomeRowRaised, raised.Envelope.Type);
        var row = _h.Events.Payload<HomeRowRaised>(raised).Value.Row;
        Assert.Equal((HomeKind.RunFailed, HomeRunRows.IdOf(failed.Id)), (row.Kind, row.Id));
        Assert.Equal("morning-check failed: step 'fetch' failed: 502", row.Title);
        Assert.False(first.Value.Idle);
        Assert.True(second.Value.Idle);
    }

    [Fact]
    public async Task A_run_marked_seen_clears_its_row()
    {
        var failed = await Fail(1, "boom");
        _ = await Tick();

        Assert.True((await _runs.MarkSeen(failed.Id, Token)).IsSuccess);
        _ = await Tick();

        Assert.Equal([EventTypes.HomeRowRaised, EventTypes.HomeRowCleared], (await Events()).Select(static e => e.Envelope.Type).ToArray());
        Assert.Null((await _view.Get(HomeRunRows.IdOf(failed.Id), Token)).Value);
    }

    [Theory]
    [InlineData(HomeState.Done)]
    [InlineData(HomeState.Muted)]
    public async Task A_row_the_owner_put_away_marks_its_run_seen_and_clears(HomeState state)
    {
        var failed = await Fail(1, "boom");
        _ = await Tick();
        var row = (await _view.Get(HomeRunRows.IdOf(failed.Id), Token)).Value!;
        _view.Put(row with { State = state, Version = 2 });

        _ = await Tick();

        Assert.Empty((await _runs.FailedUnseen(10, Token)).Value);
        Assert.Equal(EventTypes.HomeRowCleared, (await Events())[^1].Envelope.Type);
        Assert.Null((await _view.Get(HomeRunRows.IdOf(failed.Id), Token)).Value);
    }

    [Fact]
    public async Task A_snoozed_row_stays_while_its_run_is_unseen()
    {
        var failed = await Fail(1, "boom");
        _ = await Tick();
        var row = (await _view.Get(HomeRunRows.IdOf(failed.Id), Token)).Value!;
        _view.Put(row with { State = HomeState.Snoozed, SnoozeUntil = _h.Clock.GetUtcNow().AddHours(1), Version = 2 });

        var pass = await Tick();

        Assert.True(pass.Value.Idle);
        _ = Assert.Single(await Events());
    }

    [Fact]
    public async Task Succeeded_runs_test_runs_and_other_sources_rows_are_left_alone()
    {
        _ = (await _runs.Insert(Run(1, WorkflowRunStatus.Succeeded, error: null), Token)).Value;
        _ = (await _runs.Insert(Run(2, WorkflowRunStatus.Failed, "boom") with { Mode = WorkflowRunMode.Test, TestOptions = "{}" }, Token)).Value;
        _view.Put(HomeRowSamples.Row("gh:pr:jthorare/agentic-workflow#3120"));

        var pass = await Tick();

        Assert.True(pass.Value.Idle);
        Assert.Empty(await Events());
        Assert.NotNull((await _view.Get(HomeRowSamples.Row().Id, Token)).Value);
    }

    [Fact]
    public async Task Two_failures_raise_two_rows_and_a_replayed_slot_writes_nothing_twice()
    {
        _ = await Fail(1, "first");
        _ = await Fail(2, "second");
        var grant = await Grant();
        var run = new ScheduledRun(grant, _h.Clock.GetUtcNow());

        _ = await _service.Run(run, Token);
        _ = await _service.Run(run, Token);   // the view has not caught up, so the plan is the same; the keys carry the slot

        Assert.Equal(2, (await Events()).Count);
    }

    private static WorkflowRun Run(int occurrence, WorkflowRunStatus status, string? error) =>
        new(WorkflowRunId.Of(_slug, 1, occurrence), _workflow, 1, "{\"kind\":\"schedule\"}", status, Outputs: null, error, ItemId: null, KnownInstants.Epoch, KnownInstants.Epoch.AddMinutes(occurrence));

    private async Task<WorkflowRun> Fail(int occurrence, string error)
    {
        var run = Run(occurrence, WorkflowRunStatus.Failed, error);
        _ = (await _runs.Insert(run, Token)).Value;
        return run;
    }

    private async Task<LeaseGrant> Grant() => _grant ??= await _h.HoldLease();

    // One pass a minute after the last, then the projector's fold of what it wrote (the real view folds the same events).
    private async Task<Aw.Kernel.Result<ScheduledResult>> Tick()
    {
        var grant = await Grant();
        _h.Clock.Advance(TimeSpan.FromMinutes(1));
        var result = await _service.Run(new ScheduledRun(grant, _h.Clock.GetUtcNow()), Token);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        foreach (var recorded in await Events(after: _projected))
        {
            Fold(recorded);
            _projected = recorded.Sequence;
        }

        return result;
    }

    private void Fold(LedgerEvent recorded)
    {
        switch (recorded.Envelope.Type)
        {
            case EventTypes.HomeRowRaised:
                _view.Put(_h.Events.Payload<HomeRowRaised>(recorded).Value.Row);
                break;
            case EventTypes.HomeRowCleared:
                _view.Remove(_h.Events.Payload<HomeRowCleared>(recorded).Value.Id);
                break;
            default:
                break;
        }
    }

    private async Task<IReadOnlyList<LedgerEvent>> Events(long after = 0) =>
        [.. (await _h.Store.ReadAfter(after, 1000, Token)).Value.Where(static e => e.Envelope.Type.StartsWith("home.", StringComparison.Ordinal))];
}
