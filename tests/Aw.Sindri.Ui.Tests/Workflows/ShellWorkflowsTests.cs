using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The Workflows page in the shell (#2258 PR 17 part 1): G W and GoTo reach it, it reads only while it is the page shown, and leaving it returns it to the list. It is not a tab.</summary>
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
    public Task Workflows_is_not_a_tab_and_leaving_it_returns_it_to_its_list() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        Assert.DoesNotContain(shell.Navigation, n => n.Page == PageId.Workflows);
        shell.GoTo(PageId.Workflows);
        await Until(() => shell.Workflows.Rows.Count == 2);
        await shell.Workflows.OpenWorkflow(WorkflowsFixture.Slug, Token);
        Assert.True(shell.Workflows.IsRuns);

        shell.GoTo(PageId.Home);

        Assert.True(shell.Workflows.IsList);
    }, Token);
}
