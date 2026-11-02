using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>Focus on Home's Needs-you rows across a re-read (UI spec §3.3, §11.2; proof B1): rows are reconciled in place, and focus is never left on nothing.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class NeedsYouFocusTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NeedsYouFocusTests).Assembly)
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

    private static List<NeedsYouRow> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)];

    private static Border Frame(NeedsYouRow row) => row.GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal));

    private static IEnumerable<NeedsYouEntry> Parks(int n) => Enumerable.Range(0, n).Select(i => NeedsYouRows.Park("park:ITM-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public Task A_focused_row_keeps_focus_across_a_re_read() => OnUi(async () =>
    {
        var (window, shell) = await WithFeed(() => Parks(3));
        var frame = Frame(Rows(window)[1]);
        Assert.True(frame.Focus(NavigationMethod.Tab));

        shell.ShowNeedsYou(new NeedsYouFeed([.. Parks(3)], ""));
        Settle(window);
        await Task.Delay(50, Token);
        Settle(window);

        Assert.Same(frame, Focused(window));
        Assert.True(frame.IsEffectivelyVisible);
    });

    [Fact]
    public Task A_focused_row_control_keeps_focus_across_a_re_read() => OnUi(async () =>
    {
        var (window, shell) = await WithFeed(() => [NeedsYouRows.Decision()]);
        var accept = Rows(window)[0].GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("command-control"));
        Assert.True(accept.Focus(NavigationMethod.Tab));

        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision()], ""));
        Settle(window);
        await Task.Delay(50, Token);
        Settle(window);

        Assert.Same(accept, Focused(window));
    });

    [Fact]
    public Task A_cleared_focused_row_leaves_focus_on_the_next_row() => OnUi(async () =>
    {
        var (window, shell) = await WithFeed(() => Parks(3));
        Assert.True(Frame(Rows(window)[1]).Focus(NavigationMethod.Tab));

        shell.ShowNeedsYou(new NeedsYouFeed([.. Parks(3).Where(e => !string.Equals(e.Id, "park:ITM-1", StringComparison.Ordinal))], ""));
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        var focused = Focused(window);
        Assert.NotNull(focused);
        Assert.Equal("park:ITM-2", (focused.DataContext as NeedsYouRowViewModel)?.Id);
    });

    // review run 1, K13: a refresh takes focus only when it was in the row that cleared or is lost.
    [Fact]
    public Task A_row_clearing_elsewhere_does_not_pull_focus_from_the_search_field() => OnUi(async () =>
    {
        var (window, shell) = await WithFeed(() => Parks(3));
        shell.SelectNeedsYouRow("park:ITM-1");
        Settle(window);
        var search = window.GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "SearchField", StringComparison.Ordinal));
        Assert.True(search.Focus(NavigationMethod.Tab));

        shell.ShowNeedsYou(new NeedsYouFeed([.. Parks(3).Where(e => !string.Equals(e.Id, "park:ITM-1", StringComparison.Ordinal))], ""));
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.Same(search, Focused(window));
    });

    // review run 1, K21 (B1): the proof lost focus about 2 s after J, not after a Tab: the selection path.
    [Fact]
    public Task J_onto_a_row_keeps_that_frame_focused_across_a_re_read() => OnUi(async () =>
    {
        var (window, shell) = await WithFeed(() => Parks(3));
        Assert.True(shell.Press("J"));
        Settle(window);
        await Task.Delay(50, Token);
        Settle(window);
        var frame = Assert.IsType<Border>(Focused(window));

        shell.ShowNeedsYou(new NeedsYouFeed([.. Parks(3)], ""));
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.Same(frame, Focused(window));
    });

    [Fact]
    public Task X_on_a_row_whose_Decide_is_refused_leaves_focus_on_that_rows_control() => OnUi(async () =>
    {
        _server.Decided = RouteAnswer.Problem("decisions-id", "decided-elsewhere");
        var (window, shell) = await WithFeed(() => [NeedsYouRows.Decision()]);
        Assert.True(shell.Press("J"));
        Settle(window);
        await Task.Delay(50, Token);
        Settle(window);

        Assert.True(shell.Press("X"));
        await shell.Decision.Settled;
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        var focused = Focused(window);
        Assert.NotNull(focused);
        Assert.True(Rows(window)[0].IsVisualAncestorOf(focused) || ReferenceEquals(Rows(window)[0], focused));
        Assert.True(focused.IsEffectivelyEnabled && focused.IsEffectivelyVisible);
    });
}
