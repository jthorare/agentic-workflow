using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Workflows runs list in the real shell page host (#2776): a workflow with 1000 runs builds only the rows in view, never all of them.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsRunsVirtualTests : FrameHost
{
    private const int Runs = 1000;

    private static string ThousandRuns()
    {
        var list = JsonNode.Parse(GoldenFiles.Api("workflows-slug-runs", "list-three"))!.AsObject();   // why: the golden is a JSON object
        var template = list["runs"]![0]!;
        var runs = new JsonArray();
        for (var n = Runs; n >= 1; n--)
        {
            var run = template.DeepClone();
            run["id"] = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Slug}@1:{n}");
            runs.Add(run);
        }

        list["runs"] = runs;
        return list.ToJsonString();
    }

    [Fact]
    public Task Runs_list_builds_only_the_rows_in_view() => OnUi(async () =>
    {
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, ThousandRuns());
        var (window, shell) = await Connected(width: 1280, height: 800);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count == Runs, "the runs never all arrived");
        Settle(window);

        var built = window.GetVisualDescendants().OfType<Border>().Count(b => b.DataContext is RunRowViewModel && b.Classes.Contains("item-row"));
        Assert.True(built < 100, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{built} of {Runs} run rows were built; only the rows in view should be"));
    });
}
