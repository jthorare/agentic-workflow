using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3231: the workflow canvas's words follow the text size setting like the rest of the page. At 130 % every word on a card, the branch word on an edge
/// and the "done" words of an end are drawn 1.3 times as large as at 100 %, and the cards grow with them. The setting changes while the page is open.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsCanvasTextScaleFramesTests : FrameHost
{
    private const double Large = 1.3;

    // A condition between the two steps with only its "true" edge: the canvas draws a branch word on an edge, a "false → done" end and the last step's "done".
    private async Task<(Window Window, ShellViewModel Shell)> OpenWithCondition()
    {
        var workflow = Golden("workflows-slug", "found");
        var spec = workflow["spec"]!;
        spec["nodes"]!.AsArray().Insert(1, JsonNode.Parse("""{"id":"check","type":"condition","title":"Flagged?","config":{"if":"true"}}"""));
        spec["edges"] = JsonNode.Parse("""[{"from":"start","to":"check"},{"from":"check","to":"agent","output":"true"}]""");
        workflow["positions"]!.AsArray().Add(JsonNode.Parse("""{"node":"check","x":140,"y":0}"""));
        Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, workflow.ToJsonString());
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null && Visible(window, "RunsStagePanel"), "the graph never showed");
        return (window, shell);
    }

    // The canvas after a drawn frame: headless draws only when asked.
    private static GraphCanvas Drawn(Window window)
    {
        Settle(window);
        _ = window.CaptureRenderedFrame();
        return Named<GraphStage>(window, "RunsCanvas").Canvas;
    }

    private static double SizeOf(GraphCanvas canvas, string text)
    {
        var drawn = canvas.DrawnText.Where(t => string.Equals(t.Text, text, StringComparison.Ordinal)).ToList();
        Assert.True(drawn.Count > 0, $"the canvas drew no '{text}'; it drew: {string.Join(", ", canvas.DrawnText.Select(t => t.Text))}");
        return drawn[0].Size;
    }

    [Fact]
    public Task At_130_percent_every_canvas_word_is_drawn_1_3_times_as_large_and_the_cards_grow() => OnUi(async () =>
    {
        var (window, shell) = await OpenWithCondition();
        var graph = shell.Workflows.Runs.Graph!;
        var check = graph.Nodes.Single(n => string.Equals(n.Id, "check", StringComparison.Ordinal));
        string[] words = [check.Title, check.Line, "true", "false → done", "done"];
        try
        {
            shell.ApplyAppearance(new AppearanceSettings(1.0, ReduceMotion: false));
            var canvas = Drawn(window);
            var normal = words.Select(w => SizeOf(canvas, w)).ToList();
            var (width, height) = (check.Width, check.Height);

            shell.ApplyAppearance(new AppearanceSettings(Large, ReduceMotion: false));   // the setting changes while the page is open
            canvas = Drawn(window);
            var large = words.Select(w => SizeOf(canvas, w)).ToList();

            for (var i = 0; i < words.Length; i++)
            {
                Assert.True(Math.Abs(large[i] - (normal[i] * Large)) < 0.01, string.Create(CultureInfo.InvariantCulture, $"'{words[i]}' is drawn at {large[i]} px at 130 %, {normal[i]} px at 100 %"));
            }

            Assert.Equal(width * Large, check.Width, 3);
            Assert.Equal(height * Large, check.Height, 3);
            var branch = graph.Edges.Single(e => string.Equals(e.Label, "true", StringComparison.Ordinal));
            Assert.Equal(check.X + check.Width, branch.Start.X, 3);   // the edge leaves the grown card's right side
            Frame(window, "graph-scale130-1280", "workflows-");
        }
        finally
        {
            shell.ApplyAppearance(new AppearanceSettings(1.0, ReduceMotion: false));
        }
    });
}
