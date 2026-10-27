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
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Mark all seen offers Undo for SeenTracker.UndoFor on Home (proof B2): a visible "N marked seen · Undo", reachable by keyboard, announced once.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouUndoTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly OffThreadTimers _clock = new(A.Noon);   // timers fire on a pool thread, as the system clock's do (review run 1, B7)

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NeedsYouUndoTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> WithFeed(Func<IEnumerable<NeedsYouEntry>> rows)
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
        shell.ShowNeedsYou(new NeedsYouFeed([.. rows()], ""));
        Settle(window);
        return (window, shell);
    }

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private static IEnumerable<NeedsYouEntry> Parks(int n) => Enumerable.Range(0, n).Select(i => NeedsYouRows.Park("park:ITM-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static readonly Avalonia.Interactivity.RoutedEventArgs _click = new(Button.ClickEvent);

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    [Fact]
    public Task Mark_all_seen_draws_the_count_and_an_Undo_that_restores_the_rows() => OnUi(async () =>
    {
        var (window, shell) = await WithFeed(() => Parks(3));
        var offer = Named<Control>(window, "NeedsYouUndo");
        Assert.False(offer.IsEffectivelyVisible);

        Assert.Equal(3, shell.NeedsYou.Rows.Count);
        Assert.All(shell.NeedsYou.Rows, r => Assert.True(r.IsPulsing));

        Named<Button>(window, "MarkAllSeen").RaiseEvent(_click);
        Settle(window);

        Assert.All(shell.NeedsYou.Rows, r => Assert.False(r.IsPulsing));   // Mark all seen did stop them
        Assert.True(offer.IsEffectivelyVisible);
        Assert.Equal(Labels.MarkedSeen(3), Named<TextBlock>(window, "NeedsYouUndoText").Text);
        var undo = Named<Button>(window, "NeedsYouUndoButton");
        Assert.True(undo.Focusable && undo.IsTabStop && undo.IsEffectivelyEnabled);
        Assert.Equal(Labels.UndoMarkAllSeen, Avalonia.Automation.AutomationProperties.GetName(undo));

        undo.RaiseEvent(_click);
        Settle(window);

        Assert.False(offer.IsEffectivelyVisible);
        Assert.False(shell.NeedsYou.Seen.UndoOffered);
        Assert.All(shell.NeedsYou.Rows, r => Assert.True(r.IsPulsing));
    });

    [Fact]
    public Task The_summary_line_stops_before_the_marked_seen_text_instead_of_drawing_under_it() => OnUi(async () =>
    {
        var (window, _) = await WithFeed(() => Parks(3));
        window.Width = 1100;   // the narrowest width with two columns: the summary's cell is at its smallest
        Settle(window);
        Named<Button>(window, "MarkAllSeen").RaiseEvent(_click);
        Settle(window);

        var summary = Named<TextBlock>(window, "NeedsYouSummary");
        var seen = Named<TextBlock>(window, "NeedsYouUndoText");
        var summaryRight = summary.TranslatePoint(new Point(summary.Bounds.Width, 0), window)!.Value.X;   // why: both are in the shown window's tree
        var seenLeft = seen.TranslatePoint(new Point(0, 0), window)!.Value.X;   // why: both are in the shown window's tree

        Assert.True(summaryRight <= seenLeft, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the summary ends at x={summaryRight} but the marked-seen text starts at x={seenLeft}"));
    });

    [Fact]
    public Task The_offer_is_announced_once_through_a_polite_region_and_expires_after_the_undo_window() => OnUi(async () =>
    {
        var (window, _) = await WithFeed(() => Parks(2));
        var live = Named<TextBlock>(window, "NeedsYouSeenLive");
        Assert.Equal(Labels.Sindri, live.Text);
        var spoken = new List<string?>();
        live.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty && Avalonia.Automation.AutomationProperties.GetLiveSetting(live) == Avalonia.Automation.AutomationLiveSetting.Polite)
            {
                spoken.Add(live.Text);
            }
        };

        Named<Button>(window, "MarkAllSeen").RaiseEvent(_click);
        Settle(window);

        Assert.Equal([Labels.MarkedSeen(2)], spoken);
        Assert.Equal(Avalonia.Automation.AutomationLiveSetting.Polite, Avalonia.Automation.AutomationProperties.GetLiveSetting(live));

        _clock.Advance(SeenTracker.UndoFor + TimeSpan.FromSeconds(1));
        for (var i = 0; i < 50 && Named<Control>(window, "NeedsYouUndo").IsEffectivelyVisible; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.False(Named<Control>(window, "NeedsYouUndo").IsEffectivelyVisible);
        Assert.Equal(Labels.Sindri, live.Text);
    });

    [Fact]
    public Task Focus_on_Undo_moves_on_when_the_offer_goes() => OnUi(async () =>
    {
        var (window, _) = await WithFeed(() => Parks(2));
        Named<Button>(window, "MarkAllSeen").RaiseEvent(_click);
        Settle(window);
        var undo = Named<Button>(window, "NeedsYouUndoButton");
        Assert.True(undo.Focus(NavigationMethod.Tab));

        undo.RaiseEvent(_click);
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.NotNull(Focused(window));
        Assert.True(Focused(window)!.IsEffectivelyVisible);
    });
}
