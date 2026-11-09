using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Key frames for the read-only Workflows page (#2258 PR 17 part 1) at 1440 and 900 px wide, against the in-memory API serving the workflow goldens. Saved under frames/ as workflows-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsFramesTests : FrameHost
{
    private const string Prefix = "workflows-";

    private async Task<(Window Window, ShellViewModel Shell)> OnWorkflows(double width)
    {
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowRunAnswers[RunId] = RouteAnswer.Api("workflow-runs-run", "finished");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        return (window, shell);
    }

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_list_of_workflows(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(width);

        Assert.True(Visible(window, "WorkflowRows"));
        Assert.Equal(shell.Workflows.Head, Named<TextBlock>(window, "WorkflowsHead").Text);
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"list-{width}"), Prefix);
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task A_workflows_runs(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(width);

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0, "the runs never showed");

        Assert.True(Visible(window, "RunRows"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"runs-{width}"), Prefix);
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task A_runs_detail(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(width);

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await shell.Workflows.OpenRun(RunId, CancellationToken.None);
        await Until(window, () => shell.Workflows.Run.Groups.Count > 0, "the run never showed");

        Assert.True(Visible(window, "RunGroups"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"run-{width}"), Prefix);
    });
}
