using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3080: the shell's real follow path. A run's end event arrives while the runs view is still behind; the next follow beat (a throttle step of the fake clock, no new workflow event) must read the runs again while one is shown running.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsFollowBeatTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(WorkflowsFixture.Now);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        _server.WorkflowAnswers[WorkflowsFixture.Slug] = RouteAnswer.Api("workflows-slug", "found");
        _server.WorkflowRunsAnswers[WorkflowsFixture.Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        _server.WorkflowRunAnswers[WorkflowsFixture.RunId] = RouteAnswer.Api("workflow-runs-run", "finished");
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal);
        shell.GoTo(PageId.Workflows);
        await Until(() => shell.Workflows.Rows.Count == 2);
        await shell.Workflows.OpenWorkflow(WorkflowsFixture.Slug, Token);
        return shell;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    private static RouteAnswer RunsWhereTheRunningOneFailed()
    {
        var list = WorkflowsFixture.Golden("workflows-slug-runs", "list-three");
        list["runs"]![0]!["status"] = "failed";
        list["runs"]![0]!["endedAt"] = "2026-01-01T00:04:00.0000000Z";
        list["runs"]![0]!["error"] = "node 'agent' failed";
        return new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
    }

    [Fact]
    public Task A_run_that_ends_changes_its_row_and_header_on_the_next_follow_beat_with_no_new_workflow_event() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        Assert.Equal("1 running · 3 runs", shell.Workflows.Runs.Head);
        var reads = shell.Health.ViewReads;
        var endedAt = _clock.GetUtcNow();

        // The end event arrives while the runs view is behind: the follow's first re-read still sees "running".
        _server.Push(A.Event(80, "workflow.run.finished"));
        await Until(() => shell.Health.ViewReads > reads);
        Assert.Equal("1 running · 3 runs", shell.Workflows.Runs.Head);

        // The views catch up. Only a heartbeat comes next: it is not a workflow event, so the page's "moved" flag stays false.
        _server.WorkflowRunsAnswers[WorkflowsFixture.Slug] = RunsWhereTheRunningOneFailed();
        _server.Push(A.Event(81));
        var afterFirst = shell.Health.ViewReads;
        for (var step = 0; step < 3 && shell.Health.ViewReads == afterFirst; step++)
        {
            _clock.Advance(HealthViewModel.RefreshEvery);   // one throttle beat of the follower
            for (var i = 0; i < 40 && shell.Health.ViewReads == afterFirst; i++)
            {
                await Task.Delay(5, Token);
            }
        }

        Assert.True(shell.Health.ViewReads > afterFirst);
        Assert.True(_clock.GetUtcNow() - endedAt <= TimeSpan.FromSeconds(1));   // why: "within a second of the run ending", counted on the fake clock
        Assert.Equal("0 running · 3 runs", shell.Workflows.Runs.Head);
        Assert.Contains("Failed", shell.Workflows.Runs.Rows[0].ChipText, StringComparison.Ordinal);
    }, Token);
}
