using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

// The filtered runs read (#4334): the statuses, the start-time window and the 'before' cursor narrow the runs in the store's query, before the page cap.
public abstract partial class WorkflowRunStoreContract
{
    private static readonly WorkflowRunStatus[] _cycle = [WorkflowRunStatus.Succeeded, WorkflowRunStatus.Failed, WorkflowRunStatus.Cancelled, WorkflowRunStatus.Running];

    // Runs 1..count, one a minute after the epoch, their statuses cycling succeeded, failed, cancelled, running.
    private async Task<IWorkflowRunStore> Minutes(int count)
    {
        var store = Create(new FakeTimeProvider(KnownInstants.Epoch));
        for (var n = 1; n <= count; n++)
        {
            _ = (await store.Insert(Run(n, _cycle[(n - 1) % _cycle.Length]) with { StartedAt = Minute(n) }, Token)).Value;
        }

        return store;
    }

    private static DateTimeOffset Minute(int n) => KnownInstants.Epoch + TimeSpan.FromMinutes(n);

    /// <summary>A status filter keeps only the runs in those statuses, newest first.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Find_keeps_only_the_statuses_asked_for()
    {
        var store = await Minutes(8);

        var failed = (await store.Find(_workflow, WorkflowRunQuery.All with { Statuses = [WorkflowRunStatus.Failed] }, 0, 10, Token)).Value;
        var ended = (await store.Find(_workflow, WorkflowRunQuery.All with { Statuses = [WorkflowRunStatus.Failed, WorkflowRunStatus.Cancelled] }, 0, 10, Token)).Value;

        Expect.Sequence([Run(6).Id, Run(2).Id], failed.Select(static r => r.Id));
        Expect.Sequence([Run(7).Id, Run(6).Id, Run(3).Id, Run(2).Id], ended.Select(static r => r.Id));
    }

    /// <summary>'since' keeps runs that started at or after it, 'until' runs that started before it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Find_keeps_runs_that_started_in_the_window_since_inclusive_until_exclusive()
    {
        var store = await Minutes(8);

        var window = (await store.Find(_workflow, WorkflowRunQuery.All with { Since = Minute(3), Until = Minute(6) }, 0, 10, Token)).Value;
        var since = (await store.Find(_workflow, WorkflowRunQuery.All with { Since = Minute(7) }, 0, 10, Token)).Value;
        var until = (await store.Find(_workflow, WorkflowRunQuery.All with { Until = Minute(3) }, 0, 10, Token)).Value;

        Expect.Sequence([Run(5).Id, Run(4).Id, Run(3).Id], window.Select(static r => r.Id));
        Expect.Sequence([Run(8).Id, Run(7).Id], since.Select(static r => r.Id));
        Expect.Sequence([Run(2).Id, Run(1).Id], until.Select(static r => r.Id));
    }

    /// <summary>A run older than the newest 50 is found by a window around it, which a read of the newest 50 never reaches.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Find_reaches_a_run_older_than_the_newest_fifty()
    {
        var store = await Minutes(60);

        var newest = (await store.Recent(_workflow, 0, 50, Token)).Value;
        var found = (await store.Find(_workflow, WorkflowRunQuery.All with { Since = Minute(5), Until = Minute(6) }, 0, 50, Token)).Value;

        Expect.True(!newest.Any(static r => r.Id.Equals(Run(5).Id)));
        Expect.Sequence([Run(5).Id], found.Select(static r => r.Id));
    }

    /// <summary>'before' keeps the runs after that run in the order, so the last run of a page is the cursor of the next one, the start-time tie broken by occurrence.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Find_before_a_run_keeps_the_older_runs()
    {
        var store = await Minutes(5);
        _ = (await store.Insert(Run(10, WorkflowRunStatus.Succeeded) with { StartedAt = Minute(3) }, Token)).Value;

        var before = (await store.Get(Run(10).Id, Token)).Value;
        var older = (await store.Find(_workflow, WorkflowRunQuery.All with { Before = before }, 0, 10, Token)).Value;
        var failedOlder = (await store.Find(_workflow, new WorkflowRunQuery([WorkflowRunStatus.Failed], Since: null, Until: null, before), 0, 10, Token)).Value;

        Expect.Sequence([Run(3).Id, Run(2).Id, Run(1).Id], older.Select(static r => r.Id));
        Expect.Sequence([Run(2).Id], failedOlder.Select(static r => r.Id));
    }

    /// <summary>The filtered read pages with offset and limit like the unfiltered one, and never lists a test run or another workflow's run.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Find_pages_with_an_offset_and_skips_test_runs()
    {
        var store = await Minutes(8);
        _ = (await store.Insert(Run(20, WorkflowRunStatus.Failed) with { StartedAt = Minute(9), Mode = WorkflowRunMode.Test }, Token)).Value;

        var page = (await store.Find(_workflow, WorkflowRunQuery.All with { Statuses = [WorkflowRunStatus.Failed, WorkflowRunStatus.Succeeded] }, 1, 2, Token)).Value;

        Expect.Sequence([Run(5).Id, Run(2).Id], page.Select(static r => r.Id));
    }
}
