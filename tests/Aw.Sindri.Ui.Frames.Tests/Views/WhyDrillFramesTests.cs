using System.Net;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Context.Whole;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// Drilling into a concept's Why band on the desktop Context page (#1701): the pages and actions under what its decisions explain are drawn in the canvas,
/// or the page says no product evidence is linked. The canvas draws in one render pass with no control per node, so "drawn" is read as: the node is in the
/// geometry the canvas renders, its rectangle meets the canvas's viewport, and the canvas finds that node under the centre of its rectangle.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WhyDrillFramesTests : FrameHost
{
    private const string ConceptId = "concept:staffing";

    private static readonly string[] _productLabels = ["Goals page", "Set a staffing goal", "Swap board", "Offer a shift"];

    private static JsonObject Node(string key, string label) => new()
    {
        ["key"] = key,
        ["label"] = label,
        ["kind"] = key[..key.IndexOf(':', StringComparison.Ordinal)],
        ["area"] = "feature:shifts",
        ["alsoIn"] = new JsonArray(),
        ["trust"] = "trusted",
        ["provenance"] = "human",
        ["confidence"] = 1,
        ["confirmed"] = true,
        ["repo"] = "aw-split-panes",
    };

    private static JsonObject Edge(string from, string to, string type) => new()
    {
        ["from"] = from,
        ["to"] = to,
        ["type"] = type,
        ["link"] = $"edge:{from}>{to}",
        ["provenance"] = "human",
        ["confidence"] = 1,
        ["confirmed"] = true,
    };

    // The concept-open golden with its nodes and edges replaced by these.
    private static RouteAnswer Concept((string Key, string Label)[] nodes, (string From, string To, string Type)[] links)
    {
        var golden = GoldenFiles.Node("context-whole", "concept-open");
        golden["nodes"] = new JsonArray([.. nodes.Select(n => (JsonNode)Node(n.Key, n.Label))]);
        golden["edges"] = new JsonArray([.. links.Select(l => (JsonNode)Edge(l.From, l.To, l.Type))]);
        var byKind = new JsonObject();
        foreach (var kind in golden["counts"]!["byKind"]!.AsObject().Select(p => p.Key).ToList())
        {
            byKind[kind] = nodes.Count(n => n.Key.StartsWith(kind + ":", StringComparison.Ordinal));
        }

        golden["counts"] = new JsonObject { ["nodes"] = nodes.Length, ["edges"] = links.Length, ["byKind"] = byKind };
        return new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> DrilledIntoWhy(RouteAnswer concept)
    {
        Server.WholeMapAnswer = q => q.Concept is not null ? concept : ContextAnswers.WholeMap(q);
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole || shell.Context.ShowConcepts);
        Settle(window);

        await shell.Context.OpenConcept(ConceptId, Token);
        await Until(window, () => shell.Context.Whole.HasConcept && shell.Context.Whole.ShowBandButtons && window.GetVisualDescendants().OfType<WholeMapView>().Any(v => v.Canvas.Bounds.Width > 0));
        Settle(window);

        shell.Context.Whole.DrillInto(WholeBand.Intent);
        Settle(window);
        Named<WholeMapView>(window, "WholeMap").Canvas.Settle();
        Assert.True(shell.Context.Whole.IsDrilled);
        return (window, shell);
    }

    // The node with this label is placed in the drawn geometry, inside the canvas's viewport, and the canvas finds it under the middle of its card.
    private static void AssertDrawn(ShellWindow window, ShellViewModel shell, string label)
    {
        var node = shell.Context.Whole.Shown!.Nodes.SingleOrDefault(n => string.Equals(n.Label, label, StringComparison.Ordinal));
        Assert.True(node is not null, $"\"{label}\" is in the drawn geometry; drawn: {string.Join(", ", shell.Context.Whole.Shown.Nodes.Select(n => n.Label))}");
        var canvas = window.GetVisualDescendants().OfType<WholeMapCanvas>().Single();
        var viewport = new WholeRect(canvas.Viewport.X, canvas.Viewport.Y, canvas.Viewport.Width, canvas.Viewport.Height);
        Assert.True(node.Rect.Intersects(viewport), $"\"{label}\" lies outside the viewport");
        Assert.Equal(node.Key, canvas.KeyAt(new Point(node.Rect.X + (node.Rect.Width / 2), node.Rect.Y + (node.Rect.Height / 2))));
    }

    [Fact]
    public Task Drilling_into_Why_draws_the_pages_and_actions_under_what_the_decisions_explain() => OnUi(async () =>
    {
        var concept = Concept(
            [
                ("decision:why-goal", "Why a staffing goal"), ("decision:why-swaps", "Why swaps"), ("thing:staffing-goal", "Staffing goal"), ("feature:swaps", "Swaps"),
                ("surface:goals", "Goals page"), ("action:set-goal", "Set a staffing goal"), ("surface:swap-board", "Swap board"), ("action:offer-shift", "Offer a shift"),
            ],
            [
                ("decision:why-goal", "thing:staffing-goal", "decides"), ("action:set-goal", "thing:staffing-goal", "writes"), ("action:set-goal", "surface:goals", "on"),
                ("decision:why-swaps", "feature:swaps", "decides"), ("surface:swap-board", "feature:swaps", "part-of"), ("action:offer-shift", "surface:swap-board", "on"),
            ]);

        var (window, shell) = await DrilledIntoWhy(concept);

        foreach (var label in _productLabels)
        {
            AssertDrawn(window, shell, label);
        }

        Assert.False(Visible(window, "WholeDrillEmpty"));
        Frame(window, "why-drill-product", "w6-");
    });

    [Fact]
    public Task Drilling_into_Why_with_no_product_evidence_says_so() => OnUi(async () =>
    {
        var concept = Concept([("decision:lonely", "A lonely decision"), ("record:r", "A record")], [("decision:lonely", "record:r", "quotes")]);

        var (window, shell) = await DrilledIntoWhy(concept);

        Assert.True(Visible(window, "WholeDrillEmpty"));
        Assert.Equal(Labels.ContextDrillWhyNoProductLine, Named<Avalonia.Controls.TextBlock>(window, "WholeDrillEmpty").Text);
        Assert.DoesNotContain(shell.Context.Whole.Shown!.Nodes, n => NodeBands.Of(n.Kind) == WholeBand.Product);
        AssertDrawn(window, shell, "A lonely decision");
        AssertDrawn(window, shell, "A record");
        Frame(window, "why-drill-no-product", "w6-");
    });
}
