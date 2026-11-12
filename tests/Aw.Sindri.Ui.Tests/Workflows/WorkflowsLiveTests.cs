using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3080: a run that ends turns its row and the workflow header to its end state without a click, and "Started 2m ago" / "Ended 5m ago" move with the clock.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsLiveTests
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

    // list-three with its running run (the first) failed, as the runs view says once the projection has caught up.
    private static RouteAnswer RunsWhereTheRunningOneFailed()
    {
        var list = WorkflowsFixture.Golden("workflows-slug-runs", "list-three");
        list["runs"]![0]!["status"] = "failed";
        list["runs"]![0]!["endedAt"] = "2026-01-01T00:04:00.0000000Z";
        list["runs"]![0]!["error"] = "node 'agent' failed";
        return new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
    }

    [Fact]
    public Task A_run_that_ends_turns_its_row_and_the_header_to_failed_from_the_event_stream() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        var row = shell.Workflows.Runs.Rows[0];
        Assert.Equal(("1 running · 3 runs", true), (shell.Workflows.Runs.Head, row.ChipText.Contains("Running", StringComparison.Ordinal)));
        var reads = shell.Health.ViewReads;

        // The end event arrives while the runs view is still behind: the first re-read still sees "running".
        _server.Push(A.Event(80, "workflow.run.finished"));
        await Until(() => shell.Health.ViewReads > reads);
        Assert.Equal("1 running · 3 runs", shell.Workflows.Runs.Head);

        // The views catch up; the follower's next beat (no new event) must read the runs again while one is shown running.
        _server.WorkflowRunsAnswers[WorkflowsFixture.Slug] = RunsWhereTheRunningOneFailed();
        await shell.Health.RefreshViews(shell.Health.ApiUrl()!, Token);

        Assert.Equal("0 running · 3 runs", shell.Workflows.Runs.Head);
        Assert.Contains("Failed", shell.Workflows.Runs.Rows[0].ChipText, StringComparison.Ordinal);
    }, Token);

    [Fact]
    public Task Started_ago_ticks_as_the_clock_moves() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        var before = shell.Workflows.Runs.Rows[0].WhenText;
        Assert.StartsWith("Started ", before, StringComparison.Ordinal);

        _clock.Advance(TimeSpan.FromMinutes(1));

        await Until(() => !string.Equals(shell.Workflows.Runs.Rows[0].WhenText, before, StringComparison.Ordinal));
        Assert.StartsWith("Started ", shell.Workflows.Runs.Rows[0].WhenText, StringComparison.Ordinal);
    }, Token);

    [Fact]
    public Task Ended_ago_on_the_run_page_ticks() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        await shell.Workflows.OpenRun(WorkflowsFixture.RunId, Token);
        var before = shell.Workflows.Run.WhenText;
        Assert.StartsWith("Ended ", before, StringComparison.Ordinal);

        _clock.Advance(TimeSpan.FromMinutes(1));

        await Until(() => !string.Equals(shell.Workflows.Run.WhenText, before, StringComparison.Ordinal));
        Assert.StartsWith("Ended ", shell.Workflows.Run.WhenText, StringComparison.Ordinal);
    }, Token);
}
