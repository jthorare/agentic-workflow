using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The Workflows page in the shell (#2258 PR 17 part 1): G W and GoTo reach it, it is the third tab, it reads only while it is the page shown (again after a run's events), and leaving it returns it to the list.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellWorkflowsTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal);
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

    [Fact]
    public Task Going_to_Workflows_reads_the_list_and_names_the_page() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        Assert.Equal(0, _server.WorkflowsCalls);

        shell.GoTo(PageId.Workflows);
        await Until(() => shell.Workflows.Rows.Count == 2);

        Assert.Equal((true, "Workflows"), (shell.IsWorkflows, shell.PageName));
        Assert.True(_server.WorkflowsCalls > 0);
    }, Token);

    [Fact]
    public Task G_then_W_opens_Workflows() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        _ = shell.PressKey("G");
        _ = shell.PressKey("W");

        Assert.Equal(PageId.Workflows, shell.CurrentPage);
    }, Token);

    [Fact]
    public Task Workflows_is_the_third_tab_and_leaving_it_returns_it_to_its_list() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        Assert.Equal(2, shell.Navigation.ToList().FindIndex(n => n.Page == PageId.Workflows));
        var tab = shell.Navigation[2];
        Assert.Equal(("W", "Workflows"), (tab.Key, tab.DisplayLabel));
        tab.UseShort = true;
        Assert.Equal("Flows", tab.DisplayLabel);
        Assert.Equal("Workflows", tab.AccessibleName);
        shell.GoTo(PageId.Workflows);
        await Until(() => shell.Workflows.Rows.Count == 2);
        await shell.Workflows.OpenWorkflow(WorkflowsFixture.Slug, Token);
        Assert.True(shell.Workflows.IsRuns);

        shell.GoTo(PageId.Home);

        Assert.True(shell.Workflows.IsList);
    }, Token);

    [Theory]
    [InlineData("workflow.run.started")]
    [InlineData("workflow.run.finished")]
    [InlineData("workflow.run.cancelled")]
    [InlineData("workflow.node.started")]
    [InlineData("workflow.node.finished")]
    [InlineData("workflow.node.failed")]
    public Task A_workflow_run_event_reads_the_list_and_the_open_level_again(string type) => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Workflows);
        await Until(() => shell.Workflows.Rows.Count == 2);
        await shell.Workflows.OpenWorkflow(WorkflowsFixture.Slug, Token);
        var (lists, runs) = (_server.WorkflowsCalls, _server.WorkflowRunsCalls);

        _server.Push(A.Event(70, type));

        await Until(() => _server.WorkflowsCalls > lists && _server.WorkflowRunsCalls > runs);
    }, Token);

    [Fact]
    public Task A_run_event_reads_the_open_run_again() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Workflows);
        await Until(() => shell.Workflows.Rows.Count == 2);
        await shell.Workflows.OpenWorkflow(WorkflowsFixture.Slug, Token);
        await shell.Workflows.OpenRun(WorkflowsFixture.RunId, Token);
        var reads = _server.WorkflowRunCalls;

        _server.Push(A.Event(71, "workflow.run.finished"));

        await Until(() => _server.WorkflowRunCalls > reads);
    }, Token);

    [Fact]
    public Task Other_events_and_a_page_that_is_not_shown_read_nothing() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Workflows);
        await Until(() => shell.Workflows.Rows.Count == 2);
        var lists = _server.WorkflowsCalls;
        var reads = shell.Health.ViewReads;

        _server.Push(A.Event(72, "heartbeat.tick"));
        await Until(() => shell.Health.ViewReads > reads);
        Assert.Equal(lists, _server.WorkflowsCalls);

        shell.GoTo(PageId.Home);
        reads = shell.Health.ViewReads;
        _server.Push(A.Event(73, "workflow.run.finished"));
        await Until(() => shell.Health.ViewReads > reads);
        Assert.Equal(lists, _server.WorkflowsCalls);
    }, Token);
}
