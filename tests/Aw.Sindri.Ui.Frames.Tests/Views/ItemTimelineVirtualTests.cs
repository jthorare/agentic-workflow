using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The item sheet's timeline in the real shell (its own ScrollViewer above it) builds only the rows in view, however long the item's history is (#2776).</summary>
[Lane(TestLane.Integration)]
public sealed class ItemTimelineVirtualTests : FrameHost
{
    private const int Events = 1000;

    private static string Page()
    {
        var json = new StringBuilder("{\"entries\":[");
        for (var i = 1; i <= Events; i++)
        {
            json.Append(CultureInfo.InvariantCulture, $"{(i > 1 ? "," : string.Empty)}{{\"sequence\":{i},\"time\":\"2026-01-01T00:00:00.0000000Z\",\"type\":\"item.note\",\"actor\":\"human:cli\",\"line\":\"Event {i}\"}}");
        }

        return json.Append("],\"next\":null,\"empty\":null,\"asOf\":{\"cursor\":5,\"ledgerHead\":5,\"projectedAt\":\"2026-01-01T00:00:00.0000000Z\",\"rebuilding\":false,\"stalled\":false}}").ToString();
    }

    [Fact]
    public Task Item_timeline_builds_only_the_rows_in_view() => OnUi(async () =>
    {
        Server.ItemTimelinePages[(A.NewItem, 0)] = new RouteAnswer(System.Net.HttpStatusCode.OK, Page());
        var (window, shell) = await Connected(s => !s.Queue.IsLoading, width: 1280, height: 800);
        shell.OpenItem(A.NewItem);
        await Until(window, () => shell.Item.HasItem && shell.Item.Timeline.Count == Events);
        Settle(window);

        var realized = window.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("timeline-row"));

        Assert.True(realized < 100, string.Create(CultureInfo.InvariantCulture, $"the timeline built {realized} of {Events} rows"));
    });
}
