using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IWorkflowRunStore"/> honours (issue 2258, PR 6): a run id is used once, only queued, running
/// and waiting runs are in flight, a final status sets the end time, and a node run is replaced by the next write for the same
/// node and item.
/// </summary>
public abstract partial class WorkflowRunStoreContract
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("contract-drift");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh store with no runs.</summary>
    /// <param name="clock">The clock its end times come from.</param>
    /// <returns>The store.</returns>
    protected abstract IWorkflowRunStore Create(TimeProvider clock);

    /// <summary>A run of the workflow, recorded now.</summary>
    /// <param name="occurrence">Its number.</param>
    /// <param name="status">Its status.</param>
    /// <returns>The run.</returns>
    protected static WorkflowRun Run(int occurrence, WorkflowRunStatus status = WorkflowRunStatus.Running) =>
        new(WorkflowRunId.Of(_slug, 1, occurrence), _workflow, 1, "{\"kind\":\"manual\"}", status, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null);

    /// <summary>A run reads back as it was written.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_inserted_run_reads_back_as_written()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var run = Run(1);

        _ = (await store.Insert(run, Token)).Value;

        Expect.Equal(run, (await store.Get(run.Id, Token)).Value);
    }

    /// <summary>A re-delivered trigger or a catch-up overlap reuses the id: the second insert is refused and the first row stays.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_second_insert_with_the_same_run_id_is_refused_and_keeps_the_first_row()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var first = Run(1);
        _ = (await store.Insert(first, Token)).Value;

        var second = await store.Insert(first with { Trigger = "{\"kind\":\"cron\"}" }, Token);

        Expect.Equal(SindriErrors.GraphRunIdTaken.Code, second.Error.Code);
        Expect.Equal(first, (await store.Get(first.Id, Token)).Value);
        _ = Expect.Only((await store.InFlight(_workflow, Token)).Value);
    }

    /// <summary>A run that does not exist is SND-GRAPH-101.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_run_that_does_not_exist_is_not_found()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));

        var missing = await store.Get(Run(7).Id, Token);

        Expect.Equal(SindriErrors.GraphRunNotFound.Code, missing.Error.Code);
    }

    /// <summary>Queued, running and waiting runs are in flight, oldest first; a final one is not.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Only_queued_running_and_waiting_runs_are_in_flight()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1, WorkflowRunStatus.Succeeded), Token)).Value;
        _ = (await store.Insert(Run(2, WorkflowRunStatus.Waiting), Token)).Value;
        _ = (await store.Insert(Run(3, WorkflowRunStatus.Queued), Token)).Value;
        _ = (await store.Insert(Run(4, WorkflowRunStatus.Cancelled), Token)).Value;

        var inFlight = (await store.InFlight(_workflow, Token)).Value;

        Expect.Sequence([Run(2).Id, Run(3).Id], inFlight.Select(static r => r.Id));
    }

    /// <summary>The recent list holds runs of every status, newest first, cut at the limit, and none of another workflow's.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Recent_lists_the_newest_runs_of_every_status_cut_at_the_limit()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1, WorkflowRunStatus.Succeeded) with { StartedAt = KnownInstants.Epoch + TimeSpan.FromMinutes(1) }, Token)).Value;
        _ = (await store.Insert(Run(2, WorkflowRunStatus.Failed) with { StartedAt = KnownInstants.Epoch + TimeSpan.FromMinutes(2) }, Token)).Value;
        _ = (await store.Insert(Run(3, WorkflowRunStatus.Running) with { StartedAt = KnownInstants.Epoch + TimeSpan.FromMinutes(3) }, Token)).Value;
        var other = WorkflowId.From("WF-00000000000000000000000000000002");
        _ = (await store.Insert(Run(4) with { WorkflowId = other, Id = WorkflowRunId.Of(WorkflowSlug.From("another"), 1, 1) }, Token)).Value;

        var all = (await store.Recent(_workflow, 0, 10, Token)).Value;
        var two = (await store.Recent(_workflow, 0, 2, Token)).Value;

        Expect.Sequence([Run(3).Id, Run(2).Id, Run(1).Id], all.Select(static r => r.Id));
        Expect.Sequence([Run(3).Id, Run(2).Id], two.Select(static r => r.Id));
        Expect.Equal(0, (await store.Recent(WorkflowId.From("WF-00000000000000000000000000000009"), 0, 10, Token)).Value.Count);
    }

    /// <summary>An offset skips the newest runs: of three, offset 1 and limit 1 is the middle one, and an offset past the end is empty.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Recent_pages_with_an_offset()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        for (var n = 1; n <= 3; n++)
        {
            _ = (await store.Insert(Run(n, WorkflowRunStatus.Succeeded) with { StartedAt = KnownInstants.Epoch + TimeSpan.FromMinutes(n) }, Token)).Value;
        }

        var middle = (await store.Recent(_workflow, 1, 1, Token)).Value;
        var past = (await store.Recent(_workflow, 3, 5, Token)).Value;

        Expect.Sequence([Run(2).Id], middle.Select(static r => r.Id));
        Expect.Equal(0, past.Count);
    }

    /// <summary>Runs that started at the same instant list by occurrence number, so 10 comes before 9 (text order would put 9 first).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Recent_breaks_a_start_time_tie_by_occurrence_number()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(9, WorkflowRunStatus.Succeeded), Token)).Value;
        _ = (await store.Insert(Run(10, WorkflowRunStatus.Succeeded), Token)).Value;

        var runs = (await store.Recent(_workflow, 0, 10, Token)).Value;

        Expect.Sequence([Run(10).Id, Run(9).Id], runs.Select(static r => r.Id));
    }

    /// <summary>Only running runs are listed, over every workflow, oldest first.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Running_lists_only_running_runs_oldest_first()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var store = Create(clock);
        _ = (await store.Insert(Run(1, WorkflowRunStatus.Succeeded), Token)).Value;
        _ = (await store.Insert(Run(2), Token)).Value;
        _ = (await store.Insert(Run(3, WorkflowRunStatus.Queued), Token)).Value;
        _ = (await store.Insert(Run(4), Token)).Value;

        var running = (await store.Running(Token)).Value;

        Expect.Sequence([Run(2).Id, Run(4).Id], running.Select(static r => r.Id));
    }

    /// <summary>Each workflow with a queued run is listed once, and a workflow with none is not.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task QueuedWorkflows_lists_a_workflow_with_queued_runs_once_and_none_without()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1, WorkflowRunStatus.Succeeded), Token)).Value;
        Expect.Equal(0, (await store.QueuedWorkflows(Token)).Value.Count);
        _ = (await store.Insert(Run(2, WorkflowRunStatus.Queued), Token)).Value;
        _ = (await store.Insert(Run(3, WorkflowRunStatus.Queued), Token)).Value;

        var queued = (await store.QueuedWorkflows(Token)).Value;

        Expect.Sequence([_workflow], queued);
    }

    /// <summary>A final status sets the end time from the clock and keeps the outputs and the error given.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_final_status_sets_the_end_time_and_leaves_the_run_in_no_flight_list()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var store = Create(clock);
        _ = (await store.Insert(Run(1), Token)).Value;
        clock.Advance(TimeSpan.FromMinutes(5));

        var failed = (await store.SetStatus(Run(1).Id, WorkflowRunStatus.Failed, outputs: null, error: "node 'agent' failed", Token)).Value;

        Expect.Equal(WorkflowRunStatus.Failed, failed.Status);
        Expect.Equal(KnownInstants.Epoch + TimeSpan.FromMinutes(5), failed.EndedAt);
        Expect.Equal("node 'agent' failed", failed.Error);
        Expect.Equal(0, (await store.InFlight(_workflow, Token)).Value.Count);
    }

    /// <summary>Moving a run that does not exist is SND-GRAPH-101.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Setting_the_status_of_a_missing_run_is_not_found()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));

        var result = await store.SetStatus(Run(9).Id, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token);

        Expect.Equal(SindriErrors.GraphRunNotFound.Code, result.Error.Code);
    }

    /// <summary>A run is linked to the first item its work made; linking a run that does not exist is SND-GRAPH-101.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Setting_the_item_of_a_run_reads_back_and_a_missing_run_is_not_found()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var run = Run(1);
        _ = (await store.Insert(run, Token)).Value;
        var first = ItemId.From("ITM-" + new string('a', 32));
        var second = ItemId.From("ITM-" + new string('b', 32));

        var set = await store.SetItem(run.Id, first, Token);
        var replaced = await store.SetItem(run.Id, second, Token);
        var missing = await store.SetItem(Run(9).Id, first, Token);

        Expect.Equal(first.Value, set.Value.ItemId?.Value);
        Expect.Equal(first.Value, (await store.Get(run.Id, Token)).Value.ItemId?.Value);
        Expect.Equal(first.Value, replaced.Value.ItemId?.Value);
        Expect.Equal(SindriErrors.GraphRunNotFound.Code, missing.Error.Code);
    }

    /// <summary>A run keeps the first item linked to it; later links (a create_item in a loop) do not replace it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task SetItem_keeps_the_first_item()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var run = Run(1);
        _ = (await store.Insert(run, Token)).Value;
        var first = ItemId.From("ITM-" + new string('a', 32));
        var second = ItemId.From("ITM-" + new string('b', 32));

        _ = await store.SetItem(run.Id, first, Token);
        var again = await store.SetItem(run.Id, second, Token);

        Expect.Equal(first.Value, again.Value.ItemId?.Value);
        Expect.Equal(first.Value, (await store.Get(run.Id, Token)).Value.ItemId?.Value);
    }

    /// <summary>Writing a node run twice for one node and item keeps the second; another item is another row.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_node_run_write_replaces_the_row_for_the_same_node_and_item()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1), Token)).Value;
        var started = new NodeRun(Run(1).Id, NodeId.From("fetch"), 0, NodeRunStatus.Running, "{}", Output: null, Truncated: false, Error: null, Usage: null);
        _ = (await store.RecordNodeRun(started, Token)).Value;
        _ = (await store.RecordNodeRun(started with { ItemIndex = 1 }, Token)).Value;

        _ = (await store.RecordNodeRun(started with { Status = NodeRunStatus.Succeeded, Output = "{\"n\":3}" }, Token)).Value;

        var rows = (await store.NodeRuns(Run(1).Id, Token)).Value;
        Expect.Sequence([(0, NodeRunStatus.Succeeded), (1, NodeRunStatus.Running)], rows.Select(static r => (r.ItemIndex, r.Status)));
        Expect.Equal("{\"n\":3}", rows[0].Output);
    }

    /// <summary>The rows for one item are every index 0 row plus that item's rows; other items' rows are left out.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Node_runs_for_an_item_hold_index_zero_and_that_item_only()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1), Token)).Value;
        var row = new NodeRun(Run(1).Id, NodeId.From("fetch"), 0, NodeRunStatus.Succeeded, Input: null, Output: "{}", Truncated: false, Error: null, Usage: null);
        foreach (var index in new[] { 0, 1, 2, 3 })
        {
            _ = (await store.RecordNodeRun(row with { ItemIndex = index }, Token)).Value;
        }

        _ = (await store.RecordNodeRun(row with { NodeId = NodeId.From("each") }, Token)).Value;

        var rows = (await store.NodeRunsForItem(Run(1).Id, 2, Token)).Value;

        Expect.Sequence([("each", 0), ("fetch", 0), ("fetch", 2)], rows.Select(static r => (r.NodeId.Value, r.ItemIndex)));
    }

    /// <summary>A node run of a run nobody recorded is SND-GRAPH-101.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_node_run_for_a_missing_run_is_not_found()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var orphan = new NodeRun(Run(3).Id, NodeId.From("fetch"), 0, NodeRunStatus.Running, Input: null, Output: null, Truncated: false, Error: null, Usage: null);

        var result = await store.RecordNodeRun(orphan, Token);

        Expect.Equal(SindriErrors.GraphRunNotFound.Code, result.Error.Code);
    }

    /// <summary>The newest occurrence of one trigger kind, whatever the run's status; other kinds and other workflows do not count.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_latest_occurrence_is_the_highest_number_among_runs_of_one_trigger_kind()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        Expect.Equal(expected: null, (await store.LatestOccurrence(_workflow, "schedule", Token)).Value);
        _ = (await store.Insert(Run(40) with { Trigger = "{\"kind\":\"schedule\"}", Status = WorkflowRunStatus.Succeeded }, Token)).Value;
        _ = (await store.Insert(Run(9) with { Trigger = "{\"kind\":\"schedule\"}" }, Token)).Value;
        _ = (await store.Insert(Run(99), Token)).Value;

        var latest = (await store.LatestOccurrence(_workflow, "schedule", Token)).Value;

        Expect.Equal(40, latest);
        Expect.Equal(expected: null, (await store.LatestOccurrence(WorkflowId.From("WF-00000000000000000000000000000002"), "schedule", Token)).Value);
    }

    /// <summary>'one-per-workflow' with a run in flight inserts nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Admit_in_one_per_workflow_skips_while_a_run_is_in_flight()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var first = await store.Admit(Run(1), GraphConcurrency.OnePerWorkflow, Token);

        var second = await store.Admit(Run(2), GraphConcurrency.OnePerWorkflow, Token);

        Expect.Equal(GraphStartKind.Started, first.Value);
        Expect.Equal(GraphStartKind.Skipped, second.Value);
        Expect.Sequence([Run(1).Id], (await store.InFlight(_workflow, Token)).Value.Select(static r => r.Id));
        Expect.Equal(SindriErrors.GraphRunNotFound.Code, (await store.Get(Run(2).Id, Token)).Error.Code);
    }

    /// <summary>'queue' with a run in flight inserts the run as queued; a used id is still refused.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Admit_in_queue_mode_queues_while_a_run_is_in_flight()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Admit(Run(1), GraphConcurrency.Queue, Token)).Value;

        var second = await store.Admit(Run(2), GraphConcurrency.Queue, Token);
        var again = await store.Admit(Run(2), GraphConcurrency.Queue, Token);

        Expect.Equal(GraphStartKind.Queued, second.Value);
        Expect.Equal(WorkflowRunStatus.Queued, (await store.Get(Run(2).Id, Token)).Value.Status);
        Expect.Equal(SindriErrors.GraphRunIdTaken.Code, again.Error.Code);
    }

    /// <summary>The check and the insert are one step: 20 triggers racing give one run and 19 skips.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Twenty_admits_racing_in_one_per_workflow_start_exactly_one()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));

        var results = await Task.WhenAll(Enumerable.Range(1, 20).Select(n => Task.Run(async () => await store.Admit(Run(n), GraphConcurrency.OnePerWorkflow, Token), Token)));

        Expect.Equal(1, results.Count(static r => r.IsSuccess && r.Value == GraphStartKind.Started));
        Expect.Equal(19, results.Count(static r => r.IsSuccess && r.Value == GraphStartKind.Skipped));
        _ = Expect.Only((await store.InFlight(_workflow, Token)).Value);
    }

    /// <summary>The oldest queued run becomes running once nothing of the workflow is in flight.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Promote_moves_the_oldest_queued_run_to_running_and_only_one_at_a_time()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Admit(Run(1), GraphConcurrency.Queue, Token)).Value;
        _ = (await store.Admit(Run(2), GraphConcurrency.Queue, Token)).Value;
        _ = (await store.Admit(Run(3), GraphConcurrency.Queue, Token)).Value;
        _ = (await store.SetStatus(Run(1).Id, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token)).Value;

        var promoted = await store.PromoteOldestQueued(_workflow, Token);
        var again = await store.PromoteOldestQueued(_workflow, Token);

        Expect.Equal(Run(2).Id, promoted.Value?.Id);
        Expect.Equal(WorkflowRunStatus.Running, promoted.Value?.Status);
        Expect.Equal(WorkflowRunStatus.Running, (await store.Get(Run(2).Id, Token)).Value.Status);
        Expect.Equal(WorkflowRunStatus.Queued, (await store.Get(Run(3).Id, Token)).Value.Status);
        Expect.Equal(expected: null, again.Value);   // run 2 is now in flight
    }

    /// <summary>A run still running holds the queue.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Promote_does_nothing_while_a_run_is_in_flight_or_none_is_queued()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var empty = await store.PromoteOldestQueued(_workflow, Token);
        _ = (await store.Admit(Run(1), GraphConcurrency.Queue, Token)).Value;
        _ = (await store.Admit(Run(2), GraphConcurrency.Queue, Token)).Value;

        var busy = await store.PromoteOldestQueued(_workflow, Token);

        Expect.Equal(expected: null, empty.Value);
        Expect.Equal(expected: null, busy.Value);
        Expect.Equal(WorkflowRunStatus.Queued, (await store.Get(Run(2).Id, Token)).Value.Status);
    }

    /// <summary>The check and the change are one step: 20 promotes racing over 5 queued runs start exactly one.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Twenty_promotes_racing_start_exactly_one_queued_run()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        foreach (var n in Enumerable.Range(1, 5))
        {
            _ = (await store.Insert(Run(n, WorkflowRunStatus.Queued), Token)).Value;
        }

        var results = await Task.WhenAll(Enumerable.Range(1, 20).Select(_ => Task.Run(async () => await store.PromoteOldestQueued(_workflow, Token), Token)));

        Expect.Equal(1, results.Count(static r => r.IsSuccess && r.Value is not null));
        Expect.Equal(1, (await store.InFlight(_workflow, Token)).Value.Count(static r => r.Status == WorkflowRunStatus.Running));
        Expect.Equal(4, (await store.InFlight(_workflow, Token)).Value.Count(static r => r.Status == WorkflowRunStatus.Queued));
    }

    /// <summary>A run that ended is final: a later move (a cancel after the success, a begin after the cancel) answers the row as it stands and changes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_run_that_ended_is_not_moved_again()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var store = Create(clock);
        _ = (await store.Insert(Run(1), Token)).Value;
        var ended = (await store.SetStatus(Run(1).Id, WorkflowRunStatus.Succeeded, outputs: "{}", error: null, Token)).Value;
        clock.Advance(TimeSpan.FromMinutes(5));

        var cancelled = (await store.SetStatus(Run(1).Id, WorkflowRunStatus.Cancelled, outputs: null, error: "cancelled", Token)).Value;
        var begun = (await store.SetStatus(Run(1).Id, WorkflowRunStatus.Running, outputs: null, error: null, Token)).Value;

        Expect.Equal(ended, cancelled);
        Expect.Equal(ended, begun);
        Expect.Equal(ended, (await store.Get(Run(1).Id, Token)).Value);
    }

    /// <summary>A cancelled run is not started by a late begin, so one-per-workflow still sees it as not in flight.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_cancelled_run_is_not_flipped_back_to_running()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1, WorkflowRunStatus.Queued), Token)).Value;
        _ = (await store.SetStatus(Run(1).Id, WorkflowRunStatus.Cancelled, outputs: null, error: "cancelled", Token)).Value;

        var begun = (await store.SetStatus(Run(1).Id, WorkflowRunStatus.Running, outputs: null, error: null, Token)).Value;

        Expect.Equal(WorkflowRunStatus.Cancelled, begun.Status);
        Expect.Equal(0, (await store.InFlight(_workflow, Token)).Value.Count);
    }

    /// <summary>Two ends racing for one run (a success and a cancel): exactly one wins, both calls answer the winner's row, and the store keeps it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Two_ends_racing_for_one_run_leave_one_winner_that_both_calls_report()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1), Token)).Value;

        var ends = await Task.WhenAll(
            Task.Run(async () => await store.SetStatus(Run(1).Id, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token), Token),
            Task.Run(async () => await store.SetStatus(Run(1).Id, WorkflowRunStatus.Cancelled, outputs: null, error: "cancelled", Token), Token));

        var stored = (await store.Get(Run(1).Id, Token)).Value;
        Expect.Equal(stored.Status, ends[0].Value.Status);
        Expect.Equal(stored.Status, ends[1].Value.Status);
    }

    /// <summary>A launch that never reached Temporal puts a promoted run back to queued, but only a running one moves to queued.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_running_run_goes_back_to_queued_and_an_ended_one_does_not()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1), Token)).Value;
        _ = (await store.Insert(Run(2), Token)).Value;
        _ = (await store.SetStatus(Run(2).Id, WorkflowRunStatus.Failed, outputs: null, error: "x", Token)).Value;

        var back = (await store.SetStatus(Run(1).Id, WorkflowRunStatus.Queued, outputs: null, error: null, Token)).Value;
        var stays = (await store.SetStatus(Run(2).Id, WorkflowRunStatus.Queued, outputs: null, error: null, Token)).Value;

        Expect.Equal(WorkflowRunStatus.Queued, back.Status);
        Expect.Equal(WorkflowRunStatus.Failed, stays.Status);
    }

    /// <summary>A queued cancel applies only while the run is queued: a run that was promoted to running meanwhile is left alone, and the answer shows it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_queued_cancel_leaves_a_run_that_was_promoted_meanwhile_running()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1, WorkflowRunStatus.Queued), Token)).Value;
        _ = (await store.Insert(Run(2, WorkflowRunStatus.Queued), Token)).Value;
        _ = (await store.PromoteOldestQueued(_workflow, Token)).Value;   // run 1 is running now

        var promoted = (await store.CancelQueued(Run(1).Id, "cancelled while queued", Token)).Value;
        var queued = (await store.CancelQueued(Run(2).Id, "cancelled while queued", Token)).Value;

        Expect.Equal(WorkflowRunStatus.Running, promoted.Status);
        Expect.Equal(WorkflowRunStatus.Cancelled, queued.Status);
        Expect.Equal("cancelled while queued", queued.Error);
        Expect.Equal(WorkflowRunStatus.Running, (await store.Get(Run(1).Id, Token)).Value.Status);
    }

    /// <summary>A queued cancel racing a promote: whichever wins, the run is either cancelled and never promoted, or running and never cancelled.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_queued_cancel_racing_a_promote_never_leaves_a_cancelled_run_that_also_runs()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1, WorkflowRunStatus.Queued), Token)).Value;

        var promote = Task.Run(async () => (await store.PromoteOldestQueued(_workflow, Token)).Value, Token);   // started together with the cancel
        var cancel = Task.Run(async () => (await store.CancelQueued(Run(1).Id, "cancelled while queued", Token)).Value, Token);
        var promotedRun = await promote;
        _ = await cancel;

        var stored = (await store.Get(Run(1).Id, Token)).Value;
        var promoted = promotedRun is not null;
        Expect.Equal(promoted ? WorkflowRunStatus.Running : WorkflowRunStatus.Cancelled, stored.Status);
    }

    /// <summary>A run that never reached Temporal is removed only while it is running.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Discard_removes_a_running_run_and_nothing_else()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1), Token)).Value;
        _ = (await store.Insert(Run(2, WorkflowRunStatus.Queued), Token)).Value;

        var removed = (await store.Discard(Run(1).Id, Token)).Value;
        var kept = (await store.Discard(Run(2).Id, Token)).Value;
        var missing = (await store.Discard(Run(3).Id, Token)).Value;

        Expect.Equal(expected: true, removed);
        Expect.Equal(expected: false, kept);
        Expect.Equal(expected: false, missing);
        Expect.Equal(SindriErrors.GraphRunNotFound.Code, (await store.Get(Run(1).Id, Token)).Error.Code);
        Expect.Equal(WorkflowRunStatus.Queued, (await store.Get(Run(2).Id, Token)).Value.Status);
    }

    /// <summary>No tick is recorded until one is marked, and a tick belongs to its workflow.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_workflow_has_no_last_tick_until_one_is_marked_and_ticks_are_per_workflow()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var before = await store.LastTick(_workflow, Token);
        var tick = KnownInstants.Epoch.AddHours(7);

        _ = (await store.MarkTick(_workflow, tick, Token)).Value;

        Expect.Equal(expected: null, before.Value);
        Expect.Equal(tick, (await store.LastTick(_workflow, Token)).Value);
        Expect.Equal(expected: null, (await store.LastTick(WorkflowId.From("WF-00000000000000000000000000000002"), Token)).Value);
    }

    /// <summary>A tick earlier than the one on record changes nothing: the last tick only moves forward.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Marking_an_earlier_tick_keeps_the_later_one()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var later = KnownInstants.Epoch.AddDays(2);
        _ = (await store.MarkTick(_workflow, later, Token)).Value;

        _ = (await store.MarkTick(_workflow, KnownInstants.Epoch.AddDays(1), Token)).Value;
        Expect.Equal(later, (await store.LastTick(_workflow, Token)).Value);

        var latest = KnownInstants.Epoch.AddDays(3);
        _ = (await store.MarkTick(_workflow, latest, Token)).Value;
        Expect.Equal(latest, (await store.LastTick(_workflow, Token)).Value);
    }

    /// <summary>A trigger that is not a JSON object is refused when the run is recorded, so one bad row can't break the query that reads triggers by kind.</summary>
    /// <param name="trigger">The trigger text.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData("not json")]
    [InlineData("[1]")]
    [InlineData("42")]
    [InlineData("")]
    public async Task A_trigger_that_is_not_a_json_object_is_refused_by_insert_and_admit_and_nothing_is_stored(string trigger)
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));

        var inserted = await store.Insert(Run(1) with { Trigger = trigger }, Token);
        var admitted = await store.Admit(Run(2) with { Trigger = trigger }, GraphConcurrency.Unlimited, Token);

        Expect.Equal(SindriErrors.GraphInvalid.Code, inserted.Error.Code);
        Expect.Equal(SindriErrors.GraphInvalid.Code, admitted.Error.Code);
        Expect.Equal(0, (await store.InFlight(_workflow, Token)).Value.Count);
        Expect.Equal(expected: null, (await store.LatestOccurrence(_workflow, "manual", Token)).Value);
    }
}
