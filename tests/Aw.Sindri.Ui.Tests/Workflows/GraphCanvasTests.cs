using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
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
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();   // an InvalidateVisual draws only on a render tick, which headless never fires by itself
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

    [Fact]
    public async Task The_canvas_peer_has_one_child_per_node_named_by_its_name()
    {
        var graph = await WorkflowGraphViewModelTests.GraphOf(
            new WorkflowsFixture(),
            """[{"id":"check","type":"branch","config":{}},{"id":"yes","type":"run_agent","title":"Say yes","config":{}}]""",
            """[{"from":"check","to":"yes","output":"true"}]""",
            """[{"node":"check","x":0,"y":100},{"node":"yes","x":300,"y":0}]""");

        var names = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 800, Height = 500 };
            window.Show();
            Settle(window);
            try
            {
                return ControlAutomationPeer.CreatePeerForElement(canvas).GetChildren().Select(c => c.GetName()).ToArray();
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(graph.Nodes.Select(n => n.Name), names, StringComparer.Ordinal);
        Assert.Equal(2, names.Length);
    }

    [Fact]
    public async Task Pan_and_zoom_reset_for_another_workflow_and_stay_when_the_same_one_is_read_again()
    {
        const string Nodes = """[{"id":"a","type":"x","config":{}}]""";
        const string Positions = """[{"node":"a","x":0,"y":0}]""";
        var f = new WorkflowsFixture();
        var first = await WorkflowGraphViewModelTests.GraphOf(f, Nodes, "[]", Positions, slug: "one");
        var again = await WorkflowGraphViewModelTests.GraphOf(f, Nodes, "[]", Positions, slug: "one");
        var other = await WorkflowGraphViewModelTests.GraphOf(f, Nodes, "[]", Positions, slug: "two");

        var (kept, reset, afterNull) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = first, Zoom = 1.5, Pan = new Point(90, 70) };
            canvas.Graph = again;
            var kept = (canvas.Zoom, canvas.Pan);
            canvas.Graph = other;
            var reset = (canvas.Zoom, canvas.Pan);
            canvas.Zoom = 2;
            canvas.Pan = new Point(5, 5);
            canvas.Graph = null;
            canvas.Graph = other;
            return (kept, reset, (canvas.Zoom, canvas.Pan));
        });

        Assert.Equal((1.5, new Point(90, 70)), kept);
        Assert.Equal((1d, new Point(Margin, Margin)), reset);
        Assert.Equal((1d, new Point(Margin, Margin)), afterNull);
    }

    [Fact]
    public async Task Moving_the_selection_with_the_keys_pans_a_card_that_is_off_screen_into_view()
    {
        var graph = await WorkflowGraphViewModelTests.GraphOf(
            new WorkflowsFixture(),
            """[{"id":"a","type":"x","config":{}},{"id":"b","type":"x","config":{}}]""",
            "[]",
            """[{"node":"a","x":0,"y":0},{"node":"b","x":2000,"y":900}]""");

        var (before, card, size) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 800, Height = 500 };
            window.Show();
            Settle(window);
            try
            {
                _ = canvas.Focus();
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, keySymbol: null);   // selects a: already in view
                var before = canvas.Pan;
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, keySymbol: null);   // selects b: far off
                return (before, canvas.NodeRect("b")!.Value, canvas.Bounds.Size);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(new Point(Margin, Margin), before);
        Assert.True(card.Left >= 0 && card.Right <= size.Width && card.Top >= 0 && card.Bottom <= size.Height, $"the card {card} is inside {size}");
    }

    [Fact]
    public async Task The_node_peers_keep_their_identity_between_queries_and_are_rebuilt_for_another_graph()
    {
        const string Positions = """[{"node":"a","x":0,"y":0},{"node":"b","x":300,"y":0}]""";
        var f = new WorkflowsFixture();
        var first = await WorkflowGraphViewModelTests.GraphOf(f, """[{"id":"a","type":"x","config":{}},{"id":"b","type":"x","config":{}}]""", "[]", Positions, slug: "one");
        var other = await WorkflowGraphViewModelTests.GraphOf(f, """[{"id":"c","type":"x","title":"See","config":{}}]""", "[]", """[{"node":"c","x":0,"y":0}]""", slug: "two");

        var (same, swappedNames) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = first };
            var window = new Window { Content = canvas, Width = 800, Height = 500 };
            window.Show();
            Settle(window);
            try
            {
                var peer = ControlAutomationPeer.CreatePeerForElement(canvas);
                var one = peer.GetChildren();
                var two = peer.GetChildren();
                canvas.Graph = other;
                return (one.Count == two.Count && one.Zip(two).All(p => ReferenceEquals(p.First, p.Second)), peer.GetChildren().Select(c => c.GetName()).ToArray());
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(same, "two queries return the same peer instances");
        Assert.Equal(other.Nodes.Select(n => n.Name), swappedNames, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_key_move_makes_the_new_node_peer_selected_and_the_old_one_not()
    {
        var graph = await WorkflowGraphViewModelTests.GraphOf(
            new WorkflowsFixture(),
            """[{"id":"a","type":"x","config":{}},{"id":"b","type":"x","config":{}}]""",
            "[]",
            """[{"node":"a","x":0,"y":0},{"node":"b","x":300,"y":0}]""");

        var (afterFirst, afterSecond) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 800, Height = 500 };
            window.Show();
            Settle(window);
            try
            {
                var peer = ControlAutomationPeer.CreatePeerForElement(canvas);
                var items = peer.GetChildren().Cast<ISelectionItemProvider>().ToArray();
                _ = canvas.Focus();
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, keySymbol: null);
                var first = items.Select(i => i.IsSelected).ToArray();
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, keySymbol: null);
                return (first, items.Select(i => i.IsSelected).ToArray());
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal([true, false], afterFirst);
        Assert.Equal([false, true], afterSecond);
    }

    [Fact]
    public async Task Selecting_through_a_node_peer_moves_the_graph_selection()
    {
        var graph = await WorkflowGraphViewModelTests.GraphOf(
            new WorkflowsFixture(),
            """[{"id":"a","type":"x","config":{}},{"id":"b","type":"x","config":{}}]""",
            "[]",
            """[{"node":"a","x":0,"y":0},{"node":"b","x":300,"y":0}]""");

        var selected = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 800, Height = 500 };
            window.Show();
            Settle(window);
            try
            {
                ((ISelectionItemProvider)ControlAutomationPeer.CreatePeerForElement(canvas).GetChildren()[1]).Select();
                return graph.SelectedId;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal("b", selected);
    }

    [Fact]
    public async Task A_theme_switch_redraws_the_canvas()
    {
        var graph = await WorkflowGraphViewModelTests.GraphOf(
            new WorkflowsFixture(),
            """[{"id":"a","type":"x","config":{}}]""",
            "[]",
            """[{"node":"a","x":0,"y":0}]""");

        var (before, after) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 800, Height = 500, RequestedThemeVariant = ThemeVariant.Dark };
            window.Show();
            Settle(window);
            try
            {
                var before = canvas.RenderCount;
                window.RequestedThemeVariant = ThemeVariant.Light;
                Settle(window);
                return (before, canvas.RenderCount);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(after > before, string.Create(CultureInfo.InvariantCulture, $"drew {before} times before the switch and {after} after"));
    }
}
