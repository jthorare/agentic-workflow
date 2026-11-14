using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

// Test runs (spec 2026-11-12 §3.2, P4a): a run carries 'mode' and 'test_options', and the readers that mean "real runs" skip test rows.
public abstract partial class WorkflowRunStoreContract
{
    private const string Options = "{\"withModel\":true,\"withAgents\":false,\"pins\":[]}";

    private static WorkflowRun TestRun(int occurrence, WorkflowRunStatus status = WorkflowRunStatus.Running) =>
        Run(occurrence, status) with { Mode = WorkflowRunMode.Test, TestOptions = Options };

    /// <summary>A test run reads back with its mode and its options.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_test_run_reads_back_with_its_mode_and_test_options()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var run = TestRun(500);
        _ = (await store.Insert(run, Token)).Value;

        var read = (await store.Get(run.Id, Token)).Value;

        Expect.Equal(WorkflowRunMode.Test, read.Mode);
        Expect.Equal(Options, read.TestOptions);
    }

    /// <summary>A run that sets no mode is live and has no test options.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_live_run_reads_back_live_with_no_test_options()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1), Token)).Value;

        var read = (await store.Get(Run(1).Id, Token)).Value;

        Expect.Equal(WorkflowRunMode.Live, read.Mode);
        Expect.Equal(expected: null, read.TestOptions);
    }

    /// <summary>A test run in flight takes no 'one-per-workflow' slot, so a live trigger still starts.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_test_run_in_flight_does_not_block_a_live_run_in_one_per_workflow()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(TestRun(500), Token)).Value;

        var live = await store.Admit(Run(1), GraphConcurrency.OnePerWorkflow, Token);

        Expect.Equal(GraphStartKind.Started, live.Value);
    }

    /// <summary>A test run does not queue behind a live run and is not skipped by one.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_test_run_is_admitted_while_a_live_run_is_in_flight()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(1), Token)).Value;

        var test = await store.Admit(TestRun(500), GraphConcurrency.OnePerWorkflow, Token);

        Expect.Equal(GraphStartKind.Started, test.Value);
    }

    /// <summary>A live run in 'queue' mode starts at once when only a test run is in flight.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_live_run_in_queue_mode_does_not_queue_behind_a_test_run()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(TestRun(500), Token)).Value;

        var live = await store.Admit(Run(1), GraphConcurrency.Queue, Token);

        Expect.Equal(GraphStartKind.Started, live.Value);
    }

    /// <summary>A test run still shows in the in-flight list, because purge must not delete resources under a running test.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task InFlight_still_lists_a_running_test_run()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(TestRun(500), Token)).Value;

        var inFlight = (await store.InFlight(_workflow, Token)).Value;

        Expect.Sequence([TestRun(500).Id], inFlight.Select(static r => r.Id));
    }

    /// <summary>A test run's high occurrence does not move a schedule's floor.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_latest_occurrence_skips_test_runs()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(Run(40) with { Trigger = "{\"kind\":\"schedule\"}" }, Token)).Value;
        _ = (await store.Insert(TestRun(500) with { Trigger = "{\"kind\":\"schedule\"}" }, Token)).Value;

        var latest = (await store.LatestOccurrence(_workflow, "schedule", Token)).Value;

        Expect.Equal(40, latest);
    }

    /// <summary>With only test runs of a trigger kind, there is no latest occurrence.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_latest_occurrence_is_null_when_only_test_runs_exist()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(TestRun(500) with { Trigger = "{\"kind\":\"schedule\"}" }, Token)).Value;

        Expect.Equal(expected: null, (await store.LatestOccurrence(_workflow, "schedule", Token)).Value);
    }
}
