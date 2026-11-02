using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.Views.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The whole map's area labels sit in the header strip (#1646): at any zoom, including the dot zoom's enlarged labels, none reaches below its column's header rule.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapHeaderLabelTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WholeMapHeaderLabelTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    // Where each glyph run lands: its own bounds moved by every transform on the way down (the recorder puts a text's origin in a transform).
    private static IEnumerable<Rect> GlyphRuns(Drawing drawing, Matrix to)
    {
        if (drawing is GlyphRunDrawing { GlyphRun: { } run })
        {
            yield return run.Bounds.TransformToAABB(to);
        }
        else if (drawing is DrawingGroup group)
        {
            var inner = group.Transform is { } transform ? transform.Value * to : to;
            foreach (var child in group.Children.SelectMany(c => GlyphRuns(c, inner)))
            {
                yield return child;
            }
        }
    }

    private static string I(int i) => i.ToString(CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.3)]
    [InlineData(1)]
    public async Task Header_labels_never_reach_below_their_column_header_rule(double scale)
    {
        var (labels, _) = await OnUi(() =>
        {
            var areas = Enumerable.Range(0, 4).Select(i => new WholeArea("feature:" + I(i), "Self scheduling area " + I(i), "trusted", 1, new Dictionary<string, int>(StringComparer.Ordinal), Unassigned: false)).ToList();
            var nodes = areas.Select((a, i) => new WholeNode("feature:n" + I(i), "Node " + I(i), "feature", a.Key, [], "trusted", "human", 1, Confirmed: true)).ToList();
            var answer = WholeMapLayoutTests.Draft("nodes-small") with { Areas = areas, AreaEdges = [], Nodes = nodes, Edges = [] };
            var vm = new WholeMapViewModel(() => TimeZoneInfo.Utc);
            vm.Show(answer);
            var canvas = new WholeMapCanvas { Map = vm, Scale = scale };
            var group = new DrawingGroup();
            using (var context = group.Open())
            {
                canvas.Render(context);
            }

            var columns = vm.Shown?.Columns ?? throw new InvalidOperationException("no geometry shown");
            var runs = GlyphRuns(group, Matrix.Identity).ToList();
            var found = new List<(double Left, double Top, double Bottom, double Rule)>();
            foreach (var column in columns)
            {
                var rule = column.Rect.Y + WholeMapLayout.HeaderHeight;
                found.AddRange(runs.Where(b => Math.Abs(b.Left - column.Rect.X) < 2 && b.Top < rule).Select(b => (b.Left, b.Top, b.Bottom, rule)));
            }

            return (found, columns.Count);
        });

        Expect.True(labels.Count > 0, "no header glyph runs found");
        foreach (var (left, top, bottom, rule) in labels)
        {
            Expect.True(bottom <= rule, string.Create(CultureInfo.InvariantCulture, $"header label at x={left} runs from y={top} to y={bottom}, below its header rule at y={rule} (scale {scale})"));
        }
    }
}
