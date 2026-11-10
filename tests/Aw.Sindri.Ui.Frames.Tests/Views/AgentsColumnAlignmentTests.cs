using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Agents table's header and values share their columns (#2553): each value starts where its header does. 1000 px is the window's minimum, never 900.</summary>
[Lane(TestLane.Integration)]
public sealed class AgentsColumnAlignmentTests : FrameHost
{
    private static readonly (int Head, string Cell)[] _columns = [(0, "SessionRowChip"), (2, "SessionRowRepo"), (3, "SessionRowWhere"), (4, "SessionRowDoing")];

    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task Each_value_starts_under_its_header_at(int width) => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Groups.Count > 0);
        Settle(window);

        var heads = Named<Grid>(window, "AgentsColumnsGrid").Children.OfType<Control>().ToList();
        var row = window.GetVisualDescendants().OfType<SessionRow>().First(r => r.IsEffectivelyVisible);
        var checkedColumns = 0;
        foreach (var (head, cell) in _columns)
        {
            var value = Named<Control>(row, cell);
            if (!heads[head].IsEffectivelyVisible || !value.IsEffectivelyVisible)
            {
                continue;
            }

            var headX = heads[head].TranslatePoint(default, window)!.Value.X;   // why: both are in the window
            var valueX = value.TranslatePoint(default, window)!.Value.X;   // why: both are in the window
            Assert.True(Math.Abs(headX - valueX) <= 1, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{cell} starts at {valueX} but its header at {headX}"));
            checkedColumns++;
        }

        Assert.True(checkedColumns >= 3, "the status, where and doing columns show at this width");
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"agents-columns-{width}"), "l2553-");
    });
}
