using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Agents table in a 1920 px window (agents spec §2): the header and the rows use the page's width, no empty third on the right.</summary>
[Lane(TestLane.Integration)]
public sealed class AgentsPageWidthTests : FrameHost
{
    private const double Slack = 40;

    private static double RightEdge(Control control, Visual ancestor) =>
        control.TranslatePoint(new Point(control.Bounds.Width, 0), ancestor)?.X ?? double.NaN;

    [Fact]
    public Task The_header_and_the_rows_reach_the_right_edge_of_the_page_at_1920_px() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Connected(width: 1920, height: 1200);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Groups.Count > 0);

        var page = RightEdge(Named<Panel>(window, "PageContent"), window);
        var header = RightEdge(Named<Grid>(window, "AgentsColumnsGrid"), window);
        var rows = window.GetVisualDescendants().OfType<SessionRow>().Select(r => RightEdge(Named<Grid>(r, "SessionRowGrid"), window)).ToList();

        Assert.NotEmpty(rows);
        Assert.InRange(header, page - Slack, page);
        Assert.All(rows, edge => Assert.InRange(edge, page - Slack, page));
    });
}
