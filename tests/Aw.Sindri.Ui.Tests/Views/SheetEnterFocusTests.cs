using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// UI spec §11.2 with the sheet beside the page: Enter again on the row it shows moves focus to the sheet's first action,
/// and the focused row's ring stays off the sheet (proof B2).
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SheetEnterFocusTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SheetEnterFocusTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    await _stop.CancelAsync();
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OpenSheet(double width)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = width };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0);
        shell.GoTo(PageId.Work);
        Settle(window);
        for (var i = 0; i < 12 && shell.Queue.SelectedId != A.NewItem; i++)
        {
            window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        }

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => shell.IsSheetShown && shell.Item.HasItem && shell.Item.Actions.Count > 0);
        return (window, shell);
    }

    private static async Task Until(Window window, Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(condition(), "the view never reached the expected state");
    }

    private static bool InFirstAction(Window window)
    {
        var list = window.GetVisualDescendants().OfType<ItemsControl>().First(c => c.Name == "ItemActionList");
        var first = list.GetVisualDescendants().OfType<Button>().First(b => b.Name == "RunButton");
        return ReferenceEquals(window.FocusManager?.GetFocusedElement(), first);
    }

    [Fact]
    public Task Enter_again_beside_the_page_focuses_the_sheets_first_action() => OnUi(async () =>
    {
        var (window, _) = await OpenSheet(width: 1400);
        Assert.False(InFirstAction(window));

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window);

        Assert.True(InFirstAction(window));
    });

    [Fact]
    public Task Enter_on_a_different_row_opens_that_rows_item_and_leaves_focus_outside_the_sheet() => OnUi(async () =>
    {
        var (window, shell) = await OpenSheet(width: 1400);
        var first = shell.Item.ItemId;
        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.None);
        Settle(window);
        Assert.NotEqual(first, shell.Queue.SelectedId, StringComparer.Ordinal);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => string.Equals(shell.Item.ItemId, shell.Queue.SelectedId, StringComparison.Ordinal));

        Assert.False(window.Get<Border>("Sheet").IsVisualAncestorOf((Visual)window.FocusManager!.GetFocusedElement()!));
    });

    [Fact]
    public Task Enter_where_the_sheet_covers_the_page_does_not_jump_to_the_first_action() => OnUi(async () =>
    {
        var (window, shell) = await OpenSheet(width: 1000);
        _ = window.Get<Border>("Page").Focus(NavigationMethod.Tab);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window);

        Assert.True(shell.IsSheetShown);
        Assert.False(InFirstAction(window));
    });

    [Fact]
    public Task Enter_falls_back_to_the_sheet_heading_when_the_first_action_takes_no_focus() => OnUi(async () =>
    {
        var (window, _) = await OpenSheet(width: 1400);
        var list = window.GetVisualDescendants().OfType<ItemsControl>().First(c => c.Name == "ItemActionList");
        list.GetVisualDescendants().OfType<Button>().First(b => b.Name == "RunButton").IsEnabled = false;

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window);

        Assert.Same(window.Get<Control>("SheetTitle"), window.FocusManager?.GetFocusedElement());
    });

    // The item sheet is the content of the shell's sheet layer: the placeholder is hidden, the heading peer is the bare item id.
    [Fact]
    public Task An_item_link_shows_the_item_sheet_in_the_shells_sheet_content() => OnUi(async () =>
    {
        var (window, shell) = await OpenSheet(width: 1400);
        var content = window.Get<Panel>("SheetContent");
        var body = window.Get<TextBlock>("SheetBody");

        Assert.Contains(content.GetVisualDescendants(), v => v is Ui.Views.Item.ItemSheet);
        Assert.False(body.IsEffectivelyVisible);
        Assert.True(window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "ItemSheet").IsEffectivelyVisible);
        var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(window.Get<Control>("SheetTitle"));
        Assert.Equal(shell.Item.ItemId, peer.GetName());
    });

    [Fact]
    public Task The_sheets_width_stays_clamped_while_it_is_dragged_and_the_page_keeps_its_width() => OnUi(async () =>
    {
        var (window, _) = await OpenSheet(width: 1400);
        var sheet = window.Get<Border>("Sheet");
        var grip = window.Get<Thumb>("SheetGrip");

        grip.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = new Vector(-900, 0) });   // far past the widest
        Settle(window);
        var wide = sheet.Width;
        grip.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = new Vector(2000, 0) });   // far below the narrowest
        Settle(window);

        Assert.Equal((sheet.MaxWidth, sheet.MinWidth, new Thickness(0, 0, sheet.MinWidth, 0)), (wide, sheet.Width, window.Get<Border>("Page").Margin));
    });

    [Fact]
    public Task Tab_from_the_last_control_of_the_open_row_enters_the_sheet_and_within_the_row_stays_in_it() => OnUi(async () =>
    {
        var (window, shell) = await OpenSheet(width: 1400);
        var sheet = window.Get<Border>("Sheet");
        var focusedRows = new List<bool>();
        for (var i = 0; i < 8 && !IsIn(sheet, window); i++)
        {
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, keySymbol: null);
            Settle(window);
            focusedRows.Add(window.FocusManager?.GetFocusedElement() is Visual v && v.GetSelfAndVisualAncestors().OfType<Border>()
                .Any(b => b.Name == "RowFrame" && b.DataContext is ViewModels.Queue.ItemRowViewModel r && string.Equals(r.Id, shell.Item.ItemId, StringComparison.Ordinal)));
        }

        Assert.True(IsIn(sheet, window), "Tab never reached the sheet");
        Assert.True(focusedRows.Count <= 4 && focusedRows.Take(focusedRows.Count - 1).All(inRow => inRow), "Tab left the row's own controls early or went through other rows");
    });

    private static bool IsIn(Border sheet, Window window) =>
        window.FocusManager?.GetFocusedElement() is Visual focused && (sheet == focused || sheet.IsVisualAncestorOf(focused));

    [Fact]
    public Task The_title_column_keeps_its_width_with_the_sheet_beside_the_page() => OnUi(async () =>
    {
        var (window, shell) = await OpenSheet(width: 1290);
        var row = OpenRow(window, shell);

        var title = row.GetVisualDescendants().OfType<Button>().First(b => b.Name == "TitleButton");
        var column = row.GetVisualDescendants().OfType<Grid>().First(g => g.Name == "TableRow").ColumnDefinitions[0].ActualWidth;

        Assert.True(title.Bounds.Width > 0 && column >= 160, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the title is {title.Bounds.Width} wide in a {column} column"));
    });

    [Fact]
    public Task A_row_draws_its_own_focus_border_and_has_no_adorner_so_the_sheet_covers_it() => OnUi(async () =>
    {
        var (window, shell) = await OpenSheet(width: 1290);
        var row = OpenRow(window, shell);
        var frame = row.GetVisualDescendants().OfType<Border>().First(b => b.Name == "RowFrame");

        _ = frame.Focus(NavigationMethod.Tab);
        Settle(window);

        var adorners = AdornerLayer.GetAdornerLayer(frame)!.Children.Count(c => AdornerLayer.GetAdornedElement(c) == frame && c.IsEffectivelyVisible);
        var ring = frame.BorderBrush as ISolidColorBrush;
        var expected = (ISolidColorBrush)frame.FindResource("Brush.PrimaryHover")!;
        Assert.Equal((0, 2.0, expected.Color), (adorners, frame.BorderThickness.Top, ring?.Color));
        Assert.Null(frame.FocusAdorner);
    });

    [Fact]
    public Task A_focused_title_button_in_a_row_draws_its_border_in_its_own_template() => OnUi(async () =>
    {
        var (window, shell) = await OpenSheet(width: 1290);
        var title = OpenRow(window, shell).GetVisualDescendants().OfType<Button>().First(b => b.Name == "TitleButton");

        _ = title.Focus(NavigationMethod.Tab);
        Settle(window);

        var presenter = title.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
        Assert.Equal((0, 2.0), (AdornerLayer.GetAdornerLayer(title)!.Children.Count(c => AdornerLayer.GetAdornedElement(c) == title), presenter.BorderThickness.Top));
    });

    private static Control OpenRow(ShellWindow window, ShellViewModel shell) =>
        window.GetVisualDescendants().OfType<Control>()
            .First(c => c.GetType().Name == "ItemRow" && c.DataContext is ViewModels.Queue.ItemRowViewModel r && string.Equals(r.Id, shell.Item.ItemId, StringComparison.Ordinal));
}
