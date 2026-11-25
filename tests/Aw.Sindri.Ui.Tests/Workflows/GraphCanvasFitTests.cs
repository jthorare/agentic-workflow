using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A four-node graph (the Weekday check template, #2668) fits the canvas at the narrowest window: every card sits inside the canvas's visible bounds, so nothing is clipped and nothing needs a scroll nobody can see.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphCanvasFitTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphCanvasFitTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    // The page's canvas is 952 px wide in a 1000 px window and 1232 px in a 1280 px one (the shots of #2668).
    [Theory]
    [InlineData(952)]
    [InlineData(1232)]
    public async Task Every_card_of_the_weekday_check_graph_is_inside_the_canvas_at(int canvasWidth)
    {
        var graph = await WorkflowGraphViewModelTests.GraphOf(
            new WorkflowsFixture(),
            """[{"id":"when","type":"schedule","title":"Every weekday at 7:00","config":{}},{"id":"check","type":"prompt","title":"Check","config":{}},{"id":"flagged","type":"condition","title":"Flagged?","config":{}},{"id":"tell","type":"notify","title":"Tell me","config":{}}]""",
            """[{"from":"when","to":"check"},{"from":"check","to":"flagged"},{"from":"flagged","to":"tell","output":"true"}]""",
            """[{"node":"when","x":0,"y":0},{"node":"check","x":280,"y":0},{"node":"flagged","x":560,"y":0},{"node":"tell","x":840,"y":0}]""");

        var (rects, size) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = canvasWidth, Height = 320 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            try
            {
                return (graph.Nodes.Select(n => canvas.NodeRect(n.Id)!.Value).ToArray(), canvas.Bounds.Size);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.All(rects, r => Assert.True(r.Left >= 0 && r.Right <= size.Width, string.Create(CultureInfo.InvariantCulture, $"a card spans {r.Left} to {r.Right} in a canvas {size.Width} px wide")));
    }
}
