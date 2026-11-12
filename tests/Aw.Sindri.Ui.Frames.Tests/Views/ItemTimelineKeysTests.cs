using System.Globalization;
using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>After Load more, the arrow and End keys walk the item sheet's timeline rows and never leave it (#2922). Saved under frames/ as i2922-*.png when AW_UI_FRAMES=1.</summary>
[Lane(TestLane.Integration)]
public sealed class ItemTimelineKeysTests : FrameHost
{
    private static RouteAnswer Page(int first, int last, string next)
    {
        var json = new StringBuilder("{\"entries\":[");
        for (var i = first; i <= last; i++)
        {
            json.Append(CultureInfo.InvariantCulture, $"{(i > first ? "," : string.Empty)}{{\"sequence\":{i},\"time\":\"2026-01-01T00:00:00.0000000Z\",\"type\":\"item.note\",\"actor\":\"human:cli\",\"line\":\"Event {i}\"}}");
        }

        json.Append(CultureInfo.InvariantCulture, $"],\"next\":{next},\"empty\":null,\"asOf\":{{\"cursor\":5,\"ledgerHead\":5,\"projectedAt\":\"2026-01-01T00:00:00.0000000Z\",\"rebuilding\":false,\"stalled\":false}}}}");
        return new RouteAnswer(System.Net.HttpStatusCode.OK, json.ToString());
    }

    private async Task<Window> AfterLoadMore(double width = 1280)
    {
        Server.ItemTimelinePages[(A.NewItem, 0)] = Page(1, 50, "50");
        Server.ItemTimelinePages[(A.NewItem, 50)] = Page(51, 100, "null");
        var (window, shell) = await Connected(s => !s.Queue.IsLoading, width: width, height: 800);
        shell.OpenItem(A.NewItem);
        await Until(window, () => shell.Item.HasItem && shell.Item.Timeline.Count == 50);
        Settle(window);

        var more = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "LoadMoreButton");
        _ = more.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => shell.Item.Timeline.Count == 100);
        Settle(window);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Settle(window);
        return window;
    }

    private static (bool InTimeline, string Name, string Description) Focused(Window window)
    {
        var focused = window.FocusManager?.GetFocusedElement() as Control;
        var inTimeline = focused?.FindAncestorOfType<ItemsControl>(includeSelf: true)?.Name == "TimelineList";
        var name = focused is null ? string.Empty : AutomationProperties.GetName(focused) ?? string.Empty;
        var description = focused is null ? "nothing" : $"{focused.GetType().Name} '{name}'";
        return (inTimeline, name, description);
    }

    private static void AssertOnRow(Window window, string lastWords)
    {
        var (inTimeline, name, description) = Focused(window);

        Assert.True(inTimeline && name.EndsWith(lastWords, StringComparison.Ordinal), $"expected a timeline row ending '{lastWords}', focus is on {description}");
    }

    [Fact]
    public Task After_load_more_Down_moves_to_the_next_row_inside_the_timeline() => OnUi(async () =>
    {
        var window = await AfterLoadMore();
        AssertOnRow(window, "Event 51");

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Settle(window);

        AssertOnRow(window, "Event 52");
        Frame(window, "1280-down", "i2922-");
        window.Close();
    });

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task After_load_more_End_focuses_the_last_loaded_row(int width) => OnUi(async () =>
    {
        var window = await AfterLoadMore(width);

        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Settle(window);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Settle(window);

        AssertOnRow(window, "Event 100");
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"{width}-end"), "i2922-");
        window.Close();
    });
}
