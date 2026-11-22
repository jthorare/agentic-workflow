using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IRunSeenStore"/> honours (home spec §3.3, E1): only live runs that ended failed and are not marked seen are listed,
/// the latest end first, cut to the limit; marking is idempotent and a mark for a run the store lacks succeeds.
/// </summary>
public abstract class RunSeenStoreContract
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("contract-seen");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh run store and its seen store over the same runs.</summary>
    /// <param name="clock">The clock the stores read.</param>
    /// <returns>The two ports.</returns>
    protected abstract (IWorkflowRunStore Runs, IRunSeenStore Seen) Create(TimeProvider clock);

    /// <summary>A run of the workflow that ended <paramref name="minutes"/> after the epoch with <paramref name="status"/>.</summary>
    /// <param name="occurrence">Its number.</param>
    /// <param name="status">Its final status.</param>
    /// <param name="minutes">When it ended, in minutes after the epoch.</param>
    /// <returns>The run.</returns>
    protected static WorkflowRun Ended(int occurrence, WorkflowRunStatus status, int minutes) =>
        new(WorkflowRunId.Of(_slug, 1, occurrence), _workflow, 1, "{\"kind\":\"manual\"}", status, Outputs: null, Error: status == WorkflowRunStatus.Failed ? "step 'fetch' failed: 502" : null, ItemId: null,
            KnownInstants.Epoch, KnownInstants.Epoch.AddMinutes(minutes));

    /// <summary>A failed run is listed until it is marked seen.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failed_run_is_listed_until_it_is_marked_seen()
    {
        var (runs, seen) = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var failed = Ended(1, WorkflowRunStatus.Failed, 5);
        _ = (await runs.Insert(failed, Token)).Value;

        var before = (await seen.FailedUnseen(10, Token)).Value;
        Assert.True((await seen.MarkSeen(failed.Id, Token)).IsSuccess);
        var after = (await seen.FailedUnseen(10, Token)).Value;

        Assert.Equal(failed.Id, Expect.Only(before).Id);
        Assert.Empty(after);
    }

    /// <summary>Succeeded, cancelled and running runs never count, and neither does a failed test run.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Only_live_runs_that_ended_failed_are_listed()
    {
        var (runs, seen) = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await runs.Insert(Ended(1, WorkflowRunStatus.Succeeded, 1), Token)).Value;
        _ = (await runs.Insert(Ended(2, WorkflowRunStatus.Cancelled, 2), Token)).Value;
        _ = (await runs.Insert(Ended(3, WorkflowRunStatus.Failed, 3) with { Mode = WorkflowRunMode.Test, TestOptions = "{}" }, Token)).Value;
        _ = (await runs.Insert(Ended(4, WorkflowRunStatus.Running, 4) with { EndedAt = null }, Token)).Value;
        var live = Ended(5, WorkflowRunStatus.Failed, 5);
        _ = (await runs.Insert(live, Token)).Value;

        var listed = (await seen.FailedUnseen(10, Token)).Value;

        Assert.Equal(live.Id, Expect.Only(listed).Id);
    }

    /// <summary>The latest end comes first and the limit keeps the newest.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_latest_failure_comes_first_and_the_limit_keeps_the_newest()
    {
        var (runs, seen) = Create(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await runs.Insert(Ended(1, WorkflowRunStatus.Failed, 10), Token)).Value;
        _ = (await runs.Insert(Ended(2, WorkflowRunStatus.Failed, 30), Token)).Value;
        _ = (await runs.Insert(Ended(3, WorkflowRunStatus.Failed, 20), Token)).Value;

        var two = (await seen.FailedUnseen(2, Token)).Value;

        Assert.Equal([2, 3], two.Select(static r => r.Id.Occurrence).ToArray());
    }

    /// <summary>Marking a run twice, or a run the store does not hold, succeeds and changes nothing else.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Marking_twice_or_marking_an_unknown_run_succeeds()
    {
        var (runs, seen) = Create(new FakeTimeProvider(KnownInstants.Epoch));
        var failed = Ended(1, WorkflowRunStatus.Failed, 5);
        _ = (await runs.Insert(failed, Token)).Value;

        Assert.True((await seen.MarkSeen(failed.Id, Token)).IsSuccess);
        Assert.True((await seen.MarkSeen(failed.Id, Token)).IsSuccess);
        Assert.True((await seen.MarkSeen(WorkflowRunId.Of(_slug, 9, 9), Token)).IsSuccess);

        Assert.Empty((await seen.FailedUnseen(10, Token)).Value);
    }
}
