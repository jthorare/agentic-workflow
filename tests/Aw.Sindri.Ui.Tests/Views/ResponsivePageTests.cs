using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
/// Proof B8 (WCAG 2.4.11) and N9: a sheet beside the page never covers page content (the page area is the window minus the
/// sheet), the page's rows drop low-priority columns and Home stacks its panels so nothing collapses (the title column
/// stays at 160 px or more, B7), and focusing a title does not move the layout.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ResponsivePageTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ResponsivePageTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Open(double sheetWidth, PageId page, double windowWidth = 1280)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = windowWidth };
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
        await Until(window, () => shell.IsSheetShown && shell.Item.HasItem);
        shell.ResizeSheet(sheetWidth);
        shell.GoTo(page);
        Settle(window);
        return (window, shell);
    }

    private static Control Named(Window window, string name) => window.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);

    private static double Left(Visual visual, Visual root) => visual.TranslatePoint(default, root)!.Value.X;

    private static IEnumerable<Control> Stops(Visual page) =>
        page.GetVisualDescendants().OfType<Control>().Where(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled && c.Bounds.Width > 0);

    public static TheoryData<double, bool> Cases => new()
    {
        { 520, false }, { 760, false }, { 520, true }, { 760, true },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public Task Every_focus_stop_in_the_page_ends_left_of_the_sheet(double sheetWidth, bool home) => OnUi(async () =>
    {
        var (window, _) = await Open(sheetWidth, home ? PageId.Home : PageId.Work);
        var sheetLeft = Left(window.Get<Border>("Sheet"), window);
        var host = Named(window, home ? "HomePage" : "QueuePage");

        var covered = Stops(host).Where(c => Left(c, window) + c.Bounds.Width > sheetLeft + 0.5).Select(c => $"{c.GetType().Name}#{c.Name}").ToList();

        Assert.True(covered.Count == 0, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"under the sheet (x {sheetLeft}): {string.Join(", ", covered)}"));
    });

    [Theory]
    [InlineData(520)]
    [InlineData(760)]
    public Task A_queue_row_keeps_its_title_at_160_and_ends_before_the_sheet(double sheetWidth) => OnUi(async () =>
    {
        var (window, _) = await Open(sheetWidth, PageId.Work);
        var sheetLeft = Left(window.Get<Border>("Sheet"), window);
        var grid = window.GetVisualDescendants().OfType<Grid>().First(g => g.Name == "TableRow");
        var title = grid.GetVisualDescendants().OfType<Button>().First(b => b.Name == "TitleButton");
        var lastColumn = grid.ColumnDefinitions[^1];

        Assert.True(Left(grid, window) + grid.Bounds.Width <= sheetLeft + 0.5, "the row runs under the sheet");
        Assert.True(title.Bounds.Width > 0 && grid.ColumnDefinitions.Any(c => c.ActualWidth >= 160), "the title column is under 160 px");
        Assert.True(lastColumn.ActualWidth > 0, "the primary action's column is gone");
    });

    [Fact]
    public Task Home_stacks_its_panels_when_the_page_is_narrow() => OnUi(async () =>
    {
        var (window, _) = await Open(sheetWidth: 760, PageId.Home);

        var running = Named(window, "RunningPanel");
        var queued = Named(window, "QueuedPanel");

        Assert.True(Left(queued, window) <= Left(running, window) + 0.5 && queued.TranslatePoint(default, window)!.Value.Y >= running.TranslatePoint(default, window)!.Value.Y + running.Bounds.Height - 0.5,
            "the Queued panel does not sit under the Running panel");
    });

    [Fact]
    public Task Home_keeps_two_columns_when_the_page_is_wide() => OnUi(async () =>
    {
        var (window, shell) = await Open(sheetWidth: 520, PageId.Home, windowWidth: 1500);
        shell.CloseSheet();
        Settle(window);

        Assert.True(Left(Named(window, "RunningPanel"), window) > Left(Named(window, "NeedsYouPanel"), window) + 100); // why: F3 home spec: Needs you left, Running and Queued right
    });

    [Fact]
    public Task Focusing_a_title_does_not_move_the_layout() => OnUi(async () =>
    {
        var (window, _) = await Open(sheetWidth: 520, PageId.Work, windowWidth: 1500);
        var rows = window.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "TitleButton").ToList();
        var before = rows.Select(b => (b.TranslatePoint(default, window), b.Bounds.Size)).ToList();
        var next = window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "RowFrame").Select(b => b.Bounds.Height).ToList();

        _ = rows[0].Focus(NavigationMethod.Tab);
        Settle(window);

        var after = rows.Select(b => (b.TranslatePoint(default, window), b.Bounds.Size)).ToList();
        var frames = window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "RowFrame").Select(b => b.Bounds.Height).ToList();
        Assert.Equal(before, after);
        Assert.Equal(next, frames);
    });

    [Fact]
    public Task A_focused_title_draws_its_whole_ring_inside_the_button() => OnUi(async () =>
    {
        var (window, _) = await Open(sheetWidth: 520, PageId.Work, windowWidth: 1500);
        var title = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "TitleButton");
        _ = title.Focus(NavigationMethod.Tab);
        Settle(window);

        // why: the button clips its template, so a ring drawn outside its bounds shows only its corners (proof B9)
        var ring = title.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
        var box = new Rect(ring.TranslatePoint(default, title)!.Value, ring.Bounds.Size);
        Assert.True(new Rect(title.Bounds.Size).Contains(box), $"ring {box} outside the button {title.Bounds.Size}");
        Assert.Equal(new Thickness(2), ring.BorderThickness);
    });
}
