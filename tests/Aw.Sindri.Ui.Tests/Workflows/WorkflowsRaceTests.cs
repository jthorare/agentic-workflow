using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Reads in flight on the Workflows page (#2258 PR 17 part 1): an answer that comes back after a newer read was asked for, or after the page was left, is dropped.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsRaceTests
{
    private const string Other = "weekly-report";

    private const string OtherRun = "morning-check@1:2";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_workflow_answer_that_arrives_after_the_next_workflows_is_dropped()
    {
        _f.Server.WorkflowRunsAnswers[Other] = RouteAnswer.Api("workflows-slug-runs", "list-empty");
        var (first, second) = (new TaskCompletionSource(), new TaskCompletionSource());
        (_f.Server.WorkflowGates[Other], _f.Server.WorkflowGates[Slug]) = (first, second);
        var page = await _f.Shown(Token);

        var openingOther = page.OpenWorkflow(Other, Token);
        var openingSlug = page.OpenWorkflow(Slug, Token);
        second.SetResult();
        await openingSlug;
        first.SetResult();
        await openingOther;

        Assert.Equal((Slug, 3), (page.Runs.Slug, page.Runs.Rows.Count));
        Assert.Equal((false, false), (page.Runs.IsEmpty, page.Runs.IsReading));
    }

    [Fact]
    public async Task A_run_answer_that_arrives_after_the_next_run_is_dropped()
    {
        _f.Server.WorkflowRunAnswers[OtherRun] = RouteAnswer.Api("workflow-runs-run", "running");
        var (first, second) = (new TaskCompletionSource(), new TaskCompletionSource());
        (_f.Server.WorkflowRunGates[RunId], _f.Server.WorkflowRunGates[OtherRun]) = (first, second);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);

        var openingFirst = page.OpenRun(RunId, Token);
        var openingSecond = page.OpenRun(OtherRun, Token);
        second.SetResult();
        await openingSecond;
        first.SetResult();
        await openingFirst;

        Assert.Equal((OtherRun, "● Running"), (page.Run.Id, page.Run.ChipGlyph + " " + page.Run.ChipWord));
        Assert.False(page.Run.IsReading);
    }

    [Fact]
    public async Task A_list_answer_that_arrives_after_the_page_was_left_is_dropped()
    {
        var gate = new TaskCompletionSource();
        _f.Server.WorkflowsGate = gate;
        var page = _f.Page();

        var showing = page.Show(Url, Token);
        page.Leave();
        gate.SetResult();
        await showing;

        Assert.Equal((0, true, string.Empty), (page.Rows.Count, page.IsReading, page.Head));
    }

    [Fact]
    public Task A_page_shown_before_Sindri_answered_reads_its_list_once_the_shell_connects() => UiThread.Run(async () =>
    {
        var records = new InMemoryEndpointRecords();
        var server = new FakeSindriApi { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
        records.Write(A.Record());
        var shell = new ShellViewModel(records, server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        shell.GoTo(PageId.Workflows);
        Assert.Equal(0, server.WorkflowsCalls);

        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.Workflows.Rows.Count != 2; i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.Equal((2, false), (shell.Workflows.Rows.Count, shell.Workflows.IsReading));
    }, Token);
}
