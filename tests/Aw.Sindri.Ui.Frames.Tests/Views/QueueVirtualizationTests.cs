using System.Globalization;
using System.Net;
using System.Text.Json;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#2776: the Work page builds the rows in view, not every row; the page sits inside the shell's shared Pages scroller.</summary>
[Lane(TestLane.Integration)]
public sealed class QueueVirtualizationTests : FrameHost
{
    private const int Rows = 1000;

    private static RouteAnswer Thousand()
    {
        var items = Enumerable.Range(1, Rows)
            .Select(i => new ItemSummaryView(string.Create(CultureInfo.InvariantCulture, $"ITM-{i:D4}"), string.Create(CultureInfo.InvariantCulture, $"Item {i}"), "alpha", "new", Step: null, A.Noon.AddMinutes(-i), PrimaryAction: null, Actions: []))
            .ToArray();
        var view = new QueueView(
            [new QueueGroupView("queued", items, Empty: null)],
            Empty: null,
            new AsOfView(214, 214, A.Noon, Rebuilding: false, Stalled: false));
        return new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(view, SindriApiClient.Json));
    }

    [Fact]
    public Task Queue_groups_builds_only_the_rows_in_view() => OnUi(async () =>
    {
        Server.QueueView = Thousand();
        var (window, shell) = await Connected(s => !s.Queue.IsLoading && s.Queue.Total >= Rows, width: 1280, height: 800);
        shell.GoTo(PageId.Work);
        Settle(window);

        var built = window.GetVisualDescendants().OfType<Ui.Views.Queue.ItemRow>().Count();

        Assert.True(built < 100, string.Create(CultureInfo.InvariantCulture, $"{built} of {Rows} rows were built"));
    });
}
