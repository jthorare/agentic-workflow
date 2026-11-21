using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

// Failed and unseen runs (Home spec 2026-11-12 §3.3, E1): one query across every workflow, and a seen mark on the run.
public abstract partial class WorkflowRunStoreContract
{
    private static readonly WorkflowId _otherWorkflow = WorkflowId.From("WF-00000000000000000000000000000002");

    private static WorkflowRun FailedAt(int occurrence, int minute) =>
        Run(occurrence, WorkflowRunStatus.Failed) with { StartedAt = KnownInstants.Epoch + TimeSpan.FromMinutes(minute), EndedAt = KnownInstants.Epoch + TimeSpan.FromMinutes(minute + 1), Error = "node 'agent' failed" };

    /// <summary>The query lists live failed runs of every workflow, newest first, and nothing else.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task FailedUnseen_lists_failed_live_runs_of_every_workflow_newest_first()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var elsewhere = FailedAt(1, 5) with { WorkflowId = _otherWorkflow, Id = WorkflowRunId.Of(WorkflowSlug.From("another"), 1, 1) };
        _ = (await store.Insert(FailedAt(1, 1), Token)).Value;
        _ = (await store.Insert(elsewhere, Token)).Value;
        _ = (await store.Insert(FailedAt(2, 9), Token)).Value;
        _ = (await store.Insert(Run(3, WorkflowRunStatus.Succeeded), Token)).Value;
        _ = (await store.Insert(Run(4, WorkflowRunStatus.Cancelled), Token)).Value;
        _ = (await store.Insert(Run(5), Token)).Value;

        var failed = (await store.FailedUnseen(Token)).Value;

        Expect.Sequence([Run(2).Id, elsewhere.Id, Run(1).Id], failed.Select(static r => r.Id));
    }

    /// <summary>A failed test run never counts.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task FailedUnseen_skips_a_failed_test_run()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(FailedAt(500, 1) with { Mode = WorkflowRunMode.Test }, Token)).Value;

        Expect.Equal(0, (await store.FailedUnseen(Token)).Value.Count);
    }

    /// <summary>A seen run leaves the list, keeps its time, and a second mark keeps the first time.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task MarkSeen_removes_the_run_from_FailedUnseen_and_the_first_mark_wins()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await store.Insert(FailedAt(1, 1), Token)).Value;
        _ = (await store.Insert(FailedAt(2, 2), Token)).Value;
        var first = KnownInstants.Epoch + TimeSpan.FromHours(1);

        var marked = (await store.MarkSeen(Run(1).Id, first, Token)).Value;
        var again = (await store.MarkSeen(Run(1).Id, first + TimeSpan.FromHours(1), Token)).Value;

        Expect.Equal(first, marked.SeenAt);
        Expect.Equal(first, again.SeenAt);
        Expect.Equal(first, (await store.Get(Run(1).Id, Token)).Value.SeenAt);
        Expect.Sequence([Run(2).Id], (await store.FailedUnseen(Token)).Value.Select(static r => r.Id));
    }

    /// <summary>Marking a run that does not exist is SND-GRAPH-101.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task MarkSeen_of_a_missing_run_is_not_found()
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));

        var result = await store.MarkSeen(Run(9).Id, KnownInstants.Epoch, Token);

        Expect.Equal(SindriErrors.GraphRunNotFound.Code, result.Error.Code);
    }
}
