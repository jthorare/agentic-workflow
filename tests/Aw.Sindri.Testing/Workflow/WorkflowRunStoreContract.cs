using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IWorkflowRunStore"/> honours (issue 2258, PR 6): a run id is used once, only queued, running
/// and waiting runs are in flight, a final status sets the end time, and a node run is replaced by the next write for the same
/// node and item.
/// </summary>
public abstract class WorkflowRunStoreContract
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
    public async Task Two_admits_racing_in_one_per_workflow_start_exactly_one()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));

        var results = await Task.WhenAll(Enumerable.Range(1, 20).Select(n => Task.Run(async () => await store.Admit(Run(n), GraphConcurrency.OnePerWorkflow, Token), Token)));

        Expect.Equal(1, results.Count(static r => r.IsSuccess && r.Value == GraphStartKind.Started));
        Expect.Equal(19, results.Count(static r => r.IsSuccess && r.Value == GraphStartKind.Skipped));
        _ = Expect.Only((await store.InFlight(_workflow, Token)).Value);
    }
}
