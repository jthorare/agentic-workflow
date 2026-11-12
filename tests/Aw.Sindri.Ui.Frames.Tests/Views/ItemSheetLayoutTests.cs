using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.Views.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3136, rendered facts the view-model tests cannot see: no empty row under the "Blocked" heading, and a title-first header whose raw id sits only behind the copy button.</summary>
[Lane(TestLane.Integration)]
public sealed class ItemSheetLayoutTests : FrameHost
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private async Task<(Window Window, ItemSheetViewModel Sheet, FakeClipboard Clipboard)> Shown()
    {
        var clipboard = new FakeClipboard();
        var catalog = new CatalogViewModel(clipboard, new FakeTimeProvider(A.Noon));
        catalog.Show(_api, Server, (await Server.Commands(_api, Token)).Value);
        var sheet = new ItemSheetViewModel(Server, catalog, new FakeTimeProvider(A.Noon));
        await sheet.Open(_api, A.NewItem, Token);
        var window = new Window { Width = 900, Height = 900, Content = new ItemSheet { DataContext = sheet } };
        window.Show();
        Settle(window);
        return (window, sheet, clipboard);
    }

    private static double TopIn(Control child, Visual ancestor) => child.TranslatePoint(new Point(0, 0), ancestor)!.Value.Y;

    [Fact]
    public Task The_next_line_under_Blocked_starts_one_gap_below_the_heading_with_no_empty_row_between() => OnUi(async () =>
    {
        var (window, sheet, _) = await Shown();
        var blocked = Named<StackPanel>(window, "ItemBlocked");
        Assert.True(blocked.IsEffectivelyVisible);   // why: the fixture item has blocked rows, so the list is drawn
        var heading = blocked.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("eyebrow") && string.Equals(t.Text, sheet.Blocked.Heading, StringComparison.Ordinal));
        var summary = Named<Control>(window, "ItemBlockedSummary");
        var next = summary.IsEffectivelyVisible ? summary : Named<Control>(window, "ItemBlockedList");

        var gap = TopIn(next, blocked) - (TopIn(heading, blocked) + heading.Bounds.Height);

        // why: the stack's own Spacing is the only space allowed; the zero-width live region used to add a whole empty line (its height) plus a second Spacing here
        Assert.Equal(blocked.Spacing, gap, precision: 1);
        var live = Named<TextBlock>(window, "ItemBlockedAnnouncement");
        Assert.Equal(TopIn(heading, blocked), TopIn(live, blocked), precision: 1);   // the live region shares the heading's cell
        window.Close();
    });

    [Fact]
    public Task The_header_is_the_title_and_the_raw_id_is_only_behind_the_copy_button_which_copies_it() => OnUi(async () =>
    {
        var (window, sheet, clipboard) = await Shown();
        Assert.NotEqual(sheet.Title, sheet.ItemId, StringComparer.Ordinal);
        Assert.Equal(sheet.Title, Named<TextBlock>(window, "ItemTitle").Text);

        // Details are closed: no visible text holds the id.
        Assert.False(sheet.DetailsOpen);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible), t => t.Text?.Contains(sheet.ItemId, StringComparison.Ordinal) == true);

        sheet.DetailsOpen = true;
        Dispatcher.UIThread.RunJobs();
        Settle(window);

        var copy = Named<Button>(window, "ItemCopyId");
        Assert.True(copy.IsEffectivelyVisible);
        Assert.Equal(sheet.Title, Named<TextBlock>(window, "ItemTitle").Text);
        copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(sheet.ItemId, clipboard.Text);
        window.Close();
    });
}
