using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>A row becomes seen by being looked at, and the pointer holds new rows (review run 1, K3 / corr-8, a11y-1): both wired in the view.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouLookTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly OffThreadTimers _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NeedsYouLookTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> WithRows(int n)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 300 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.Equal(ShellState.Normal, shell.State);
        shell.ShowNeedsYou(new NeedsYouFeed([.. Enumerable.Range(0, n).Select(i => NeedsYouRows.Park("park:ITM-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)))], ""));
        Settle(window);
        return (window, shell);
    }

    private static List<NeedsYouRow> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)];

    // The headless platform has no way to deactivate a window, so the platform callback the window subscribed to is invoked.
    private static void Raise(object impl, string callback) =>
        ((Action?)impl.GetType().GetProperty(callback, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(impl))?.Invoke();

    private static Border Frame(NeedsYouRow row) => row.GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal));

    [Fact]
    public Task A_row_focused_for_a_second_stops_pulsing() => OnUi(async () =>
    {
        var (window, shell) = await WithRows(2);
        var row = Rows(window)[0];
        var vm = (NeedsYouRowViewModel)row.DataContext!;
        Assert.True(vm.IsPulsing);

        Assert.True(Frame(row).Focus(NavigationMethod.Tab));
        Settle(window);
        _clock.Advance(SeenTracker.OnScreenFor);
        for (var i = 0; i < 50 && vm.IsPulsing; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.False(vm.IsPulsing);
        Assert.True(Rows(window)[1].DataContext is NeedsYouRowViewModel { IsPulsing: true });
        Assert.NotNull(shell);
    });

    [Fact]
    public Task A_row_focused_in_an_inactive_window_stays_unseen_until_the_window_is_active() => OnUi(async () =>
    {
        var (window, _) = await WithRows(2);
        var row = Rows(window)[0];
        var vm = (NeedsYouRowViewModel)row.DataContext!;
        Assert.True(Frame(row).Focus(NavigationMethod.Tab));
        Settle(window);
        var impl = window.PlatformImpl!;
        Raise(impl, "Deactivated");
        Settle(window);
        Assert.False(window.IsActive);

        _clock.Advance(SeenTracker.OnScreenFor);
        await Task.Delay(50, Token);
        Settle(window);
        Assert.True(vm.IsPulsing);

        Raise(impl, "Activated");
        Settle(window);
        Assert.True(window.IsActive, "active");
        Assert.NotNull(window.FocusManager?.GetFocusedElement());
        _clock.Advance(SeenTracker.OnScreenFor);
        for (var i = 0; i < 50 && vm.IsPulsing; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.False(vm.IsPulsing);
    });

    [Fact]
    public Task A_row_focused_for_less_than_a_second_keeps_pulsing() => OnUi(async () =>
    {
        var (window, _) = await WithRows(2);
        var row = Rows(window)[0];
        var vm = (NeedsYouRowViewModel)row.DataContext!;

        Assert.True(Frame(row).Focus(NavigationMethod.Tab));
        Settle(window);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.True(Frame(Rows(window)[1]).Focus(NavigationMethod.Tab));   // focus moves on: the half second is lost
        Settle(window);
        _clock.Advance(TimeSpan.FromMilliseconds(600));
        await Task.Delay(50, Token);
        Settle(window);

        Assert.True(vm.IsPulsing);
    });

    [Fact]
    public Task The_pointer_over_the_panel_holds_new_rows() => OnUi(async () =>
    {
        var (window, shell) = await WithRows(2);
        var panel = window.GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "NeedsYouPanel", StringComparison.Ordinal));
        var inside = panel.TranslatePoint(new Point(panel.Bounds.Width / 2, panel.Bounds.Height / 2), window)!.Value;

        window.MouseMove(inside, RawInputModifiers.None);
        Settle(window);
        Assert.True(shell.NeedsYou.PointerOver);

        window.MouseMove(new Point(inside.X, window.Bounds.Height - 1), RawInputModifiers.None);
        Settle(window);
        Assert.False(shell.NeedsYou.PointerOver);
    });
}
