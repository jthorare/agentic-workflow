using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The graph canvas (#2258 PR 17 part 2) in a headless window: cards at the served positions, branch labels at the middle of their curves, zoom held to 0.5 to 2.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphCanvasTests
{
    private const double Margin = 24;   // the canvas's default pan

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphCanvasTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task Nodes_and_edges_render_at_the_positions_the_server_returned_and_branch_labels_show()
    {
        var graph = await WorkflowGraphViewModelTests.GraphOf(
            new WorkflowsFixture(),
            """[{"id":"check","type":"branch","config":{}},{"id":"yes","type":"run_agent","title":"Say yes","config":{}},{"id":"no","type":"run_agent","config":{}}]""",
            """[{"from":"check","to":"yes","output":"true"},{"from":"check","to":"no","output":"false"}]""",
            """[{"node":"check","x":0,"y":100},{"node":"yes","x":300,"y":0},{"node":"no","x":300,"y":200}]""");

        var (rects, labels, size) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 800, Height = 500 };
            window.Show();
            Settle(window);
            try
            {
                return (
                    graph.Nodes.Select(n => canvas.NodeRect(n.Id)).ToArray(),
                    graph.Edges.Where(e => e.Label is not null).Select(e => (e.Label, canvas.LabelCenter(e))).ToArray(),
                    canvas.Bounds.Size);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(new Size(800, 500), size);
        Assert.Equal(
            [new Rect(Margin, Margin + 100, 200, 64), new Rect(Margin + 300, Margin, 200, 64), new Rect(Margin + 300, Margin + 200, 200, 64)],
            rects.Select(r => r!.Value));

        // Both labels sit between the cards they join: right of the source card's edge, left of the target's.
        Assert.Equal(["true", "false"], labels.Select(l => l.Label), StringComparer.Ordinal);
        Assert.All(labels, l => Assert.InRange(l.Item2.X, Margin + 200, Margin + 300));
        Assert.True(labels[0].Item2.Y < labels[1].Item2.Y, "the true branch leaves upward, the false one downward");
    }

    [Fact]
    public async Task Zoom_is_held_between_half_and_double()
    {
        var zooms = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Zoom = 5 };
            var high = canvas.Zoom;
            canvas.Zoom = 0.1;
            return (high, canvas.Zoom);
        });

        Assert.Equal((2d, 0.5d), zooms);
    }
}
