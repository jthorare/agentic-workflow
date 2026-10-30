using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Queue;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>
/// Focus and selection on the queue, Home's panels and the item sheet (UI spec §3.3, §8, §11.2) under the headless
/// platform: a refresh never moves focus, one selection is the focus, the sheet returns focus to its row.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class QueueFocusTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty"), WorkflowAnswer = RouteAnswer.Api("workflow-instances", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(QueueFocusTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected(double? width = null)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        if (width is { } w)
        {
            window.Width = w;
        }

        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task Until(Window window, Func<bool> condition, string what = "the view never reached the expected state")
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(condition(), what);
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private static List<ItemRow> Rows(Window window) =>
        [.. Named<Control>(window, "QueuePage").GetVisualDescendants().OfType<ItemRow>().Where(r => r.IsEffectivelyVisible)];

    private static void Renamed(FakeSindriApi server, string from, string to) =>
        server.QueueView = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("queue", "fixture").Replace(from, to, StringComparison.Ordinal));

    // L1 (§3.3): a re-served queue keeps the focused control: the row is updated in place, not rebuilt.
    [Fact]
    public Task A_refresh_keeps_focus_on_the_same_row_control() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Work);
        Settle(window);
        var title = Named<Button>(Rows(window)[1], "TitleButton");
        Assert.True(title.Focus(NavigationMethod.Tab));
        var focusedId = Rows(window)[1].ItemId;

        Renamed(_server, "Try the queue", "Renamed item");
        _server.Push(A.Event(58, "item.state-changed"));
        await Until(window, () => shell.Queue.Groups[3].Rows.Any(r => r.Title == "Renamed item"));

        Assert.Same(title, Focused(window));
        Assert.True(title.IsEffectivelyVisible);
        Assert.Equal(focusedId, Rows(window)[1].ItemId);
    });

    // L1: the same for a focused timeline row, the item sheet re-read on the same event.
    [Fact]
    public Task A_refresh_keeps_focus_on_the_same_timeline_row() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.OpenItem(A.NewItem);
        await Until(window, () => shell.Item.HasItem && shell.Item.Timeline.Count > 2);
        var rows = Named<Control>(window, "TimelineList").GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("timeline-row")).ToList();
        var row = rows[1];
        Assert.True(row.Focus(NavigationMethod.Tab));
        var calls = _server.TimelineCalls;

        _server.Items[A.NewItem] = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("items-id", "new").Replace("Try the queue", "Renamed item", StringComparison.Ordinal));
        _server.Push(A.Event(58, "item.state-changed"));
        await Until(window, () => shell.Item.Title == "Renamed item" && _server.TimelineCalls > calls);

        Assert.Same(row, Focused(window));
        Assert.True(row.IsEffectivelyVisible);
    });

    // L3 (§3.3, §11.2): the row that clears is spoken once through the gated polite region, and focus moves to the row now in its place.
    [Fact]
    public Task A_cleared_row_is_spoken_once_and_focus_moves_to_the_next_row() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Work);
        Settle(window);
        Assert.True(Named<Border>(Rows(window)[1], "RowFrame").Focus(NavigationMethod.Tab));
        var gone = ((ItemRowViewModel)Focused(window)!.DataContext!).Id;   // the focused row, and focus selected it
        Assert.Equal(gone, shell.Queue.SelectedId);
        var live = Named<TextBlock>(window, "QueueLive");
        Assert.Equal(Labels.Sindri, live.Text);

        _server.QueueView = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("queue", "fixture").Replace(gone, "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a3ff", StringComparison.Ordinal));
        _server.Push(A.Event(58, "item.state-changed"));
        await Until(window, () => shell.Queue.SelectedId == "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a3ff");

        Assert.Equal((Labels.Cleared(gone), Avalonia.Automation.AutomationLiveSetting.Polite), (live.Text, Avalonia.Automation.AutomationProperties.GetLiveSetting(live)));
        await Until(window, () => Focused(window) is Border frame && string.Equals(frame.Name, "RowFrame", StringComparison.Ordinal) && (frame.DataContext as ItemRowViewModel)?.Id == shell.Queue.SelectedId);
    });

    // The queue served with one queued item moved to the running group (its row is rebuilt in the other group).
    private static RouteAnswer Moved(string id)
    {
        var view = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.SpecShaped("queue", "fixture"))!.AsObject();
        var groups = view["groups"]!.AsArray();
        var queued = groups.First(g => g!["group"]!.GetValue<string>() == "queued")!["items"]!.AsArray();
        var running = groups.First(g => g!["group"]!.GetValue<string>() == "running")!["items"]!.AsArray();
        var item = queued.First(i => i!["id"]!.GetValue<string>() == id)!;
        _ = queued.Remove(item);
        running.Add(item);
        return new RouteAnswer(System.Net.HttpStatusCode.OK, view.ToJsonString());
    }

    // a11y-1: the focused row served in another group is rebuilt there: focus follows it, keyed by the same id.
    [Fact]
    public Task A_focused_row_that_changes_group_keeps_focus_on_its_new_frame() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Work);
        Settle(window);
        var moving = Rows(window)[1].ItemId;
        Assert.True(Named<Border>(Rows(window)[1], "RowFrame").Focus(NavigationMethod.Tab));

        _server.QueueView = Moved(moving);
        _server.Push(A.Event(58, "item.state-changed"));
        await Until(window, () => shell.Queue.Groups.First(g => g.Group == "running").Rows.Any(r => r.Id == moving));

        await Until(window, () => OnRowFrame(window, moving), "focus did not follow the row into its new group");
    });

    // a11y-3: the pill hides when the pointer leaves while it has focus; focus must not be left on nothing.
    [Fact]
    public Task Focus_on_the_held_pill_moves_on_when_the_pill_hides() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        Settle(window);
        shell.Queue.Hold(holding: true);
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        _server.Push(A.Event(58, "item.state-changed"));
        await Until(window, () => shell.Queue.HasHeld);
        var pill = Named<Button>(window, "HomeHeldPill");
        Assert.True(pill.Focus(NavigationMethod.Tab));

        shell.Queue.Hold(holding: false);
        await Until(window, () => !pill.IsEffectivelyVisible && Focused(window) is { IsEffectivelyVisible: true } && !ReferenceEquals(Focused(window), pill), "focus was left on the hidden pill");
    });

    // testing-0: Home passes QueueScope.Home. With the Queue page filtered to Running (G R) and Home shown again (G H), a
    // cleared queued row on Home still passes to the next queued row, and focus follows it.
    [Fact]
    public Task A_cleared_queued_row_on_home_under_the_running_filter_passes_to_the_next_queued_row() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoToQueue(QueueFilter.Running);
        shell.GoTo(PageId.Home);
        Settle(window);
        var panel = Named<Control>(window, "QueuedPanel");
        var row = panel.GetVisualDescendants().OfType<CompactItemRow>().First(r => r.IsEffectivelyVisible);
        Assert.True(Named<Border>(row, "RowFrame").Focus(NavigationMethod.Tab));
        var gone = shell.Queue.SelectedId!;
        var queued = shell.Queue.QueuedRows.Select(r => r.Id).ToList();
        var next = queued[queued.IndexOf(gone) + 1];

        var view = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.SpecShaped("queue", "fixture"))!;
        foreach (var group in view["groups"]!.AsArray())
        {
            var items = group!["items"]!.AsArray();
            foreach (var item in items.Where(i => i!["id"]!.GetValue<string>() == gone).ToList())
            {
                _ = items.Remove(item);
            }
        }

        _server.QueueView = new RouteAnswer(System.Net.HttpStatusCode.OK, view.ToJsonString());
        _server.Push(A.Event(58, "item.state-changed"));
        await Until(window, () => shell.Queue.QueuedRows.All(r => r.Id != gone));

        Assert.Equal(next, shell.Queue.SelectedId);
        await Until(window, () => FocusedRowId(window) == next, "focus did not follow the selection to the next queued row");
    });

    // testing-5 / corr-3: pressing the "N new" pill puts focus on the first row it showed, and moves it once (not to the page region first).
    [Fact]
    public Task Pressing_the_held_pill_moves_focus_once_onto_the_first_new_row() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        Settle(window);
        shell.Queue.Hold(holding: true);
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        _server.Push(A.Event(58, "item.state-changed"));
        await Until(window, () => shell.Queue.HasHeld);
        var pill = Named<Button>(window, "HomeHeldPill");
        Assert.True(pill.Focus(NavigationMethod.Tab));
        var moves = new List<Control?>();
        window.AddHandler(InputElement.GotFocusEvent, (_, e) => moves.Add(e.Source as Control));

        pill.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => !shell.Queue.HasHeld && shell.Queue.RunningRows.Count > 0);
        var first = shell.Queue.RunningRows[0].Id;
        await Until(window, () => OnRowFrame(window, first), "focus did not reach the first new row");
        await Task.Delay(50, Token);
        Settle(window);

        Assert.Equal("RowFrame", Assert.Single(moves)?.Name);
    });

    private static string? FocusedRowId(Window window) =>
        Focused(window)?.GetSelfAndVisualAncestors().OfType<Control>().Select(c => c.DataContext).OfType<ItemRowViewModel>().FirstOrDefault()?.Id;

    // L2 (§3.3, §8): J / K move the selection and focus that row's frame; Enter acts on the focused row.
    [Fact]
    public Task J_moves_the_selection_and_the_focus_together_and_enter_opens_that_row() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Work);
        Settle(window);

        window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        await Until(window, () => shell.Queue.SelectedId is not null && FocusedRowId(window) == shell.Queue.SelectedId);
        var first = shell.Queue.SelectedId;
        window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        await Until(window, () => shell.Queue.SelectedId != first && FocusedRowId(window) == shell.Queue.SelectedId);
        Assert.True(Focused(window) is Border frame && string.Equals(frame.Name, "RowFrame", StringComparison.Ordinal));

        var second = shell.Queue.SelectedId;
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => shell.IsSheetShown);
        Assert.Equal(second, shell.SheetSubject);
    });

    // L2: a row that gets focus (Tab, a click) is the selected one, so Enter and Y act on what the user is on.
    [Fact]
    public Task A_row_getting_focus_selects_it() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Work);
        Settle(window);
        var third = Rows(window)[2];

        Assert.True(Named<Border>(third, "RowFrame").Focus(NavigationMethod.Tab));
        Assert.Equal(third.ItemId, shell.Queue.SelectedId);

        var fourth = Rows(window)[3];
        Assert.True(Named<Button>(fourth, "TitleButton").Focus(NavigationMethod.Tab));
        Assert.Equal(fourth.ItemId, shell.Queue.SelectedId);
        Assert.True(shell.Queue.Groups[3].Rows[3].IsSelected);
        Assert.False(shell.Queue.Groups[3].Rows[2].IsSelected);
    });

    // L2: Home has its own rows; J there walks and focuses Home's compact rows, not the Queue page's.
    [Fact]
    public Task J_on_home_focuses_the_compact_rows_it_draws() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        Settle(window);

        window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        await Until(window, () => shell.Queue.SelectedId == shell.Queue.QueuedRows[0].Id && FocusedRowId(window) == shell.Queue.SelectedId);

        Assert.NotNull(Focused(window).FindAncestorOfType<CompactItemRow>());
    });

    // L2 (§11.2): keys are scoped to the focused region: with focus in the sheet, J does not drive the queue behind it.
    [Fact]
    public Task J_in_the_sheet_does_not_drive_the_queue() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1000);
        shell.GoTo(PageId.Work);
        Settle(window);
        window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        await Until(window, () => shell.Queue.SelectedId is not null);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => shell.IsSheetShown && string.Equals(Focused(window)?.Name, "SheetTitle", StringComparison.Ordinal));
        var selected = shell.Queue.SelectedId;

        window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        await Task.Delay(50, Token);
        Settle(window);

        Assert.Equal(selected, shell.Queue.SelectedId);
    });

    private static bool OnRowFrame(Window window, string id) =>
        Focused(window) is Border frame && string.Equals(frame.Name, "RowFrame", StringComparison.Ordinal) && (frame.DataContext as ItemRowViewModel)?.Id == id;

    // L5 (§11.2): the sheet that covers the page takes focus (its title); Esc gives it back to the row that opened it, by id.
    [Fact]
    public Task Esc_from_the_covering_sheet_returns_focus_to_the_row_that_opened_it() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1000);
        shell.GoTo(PageId.Work);
        Settle(window);
        window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        await Until(window, () => shell.Queue.SelectedId is not null && FocusedRowId(window) == shell.Queue.SelectedId);
        var opener = shell.Queue.SelectedId!;

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => shell.IsSheetShown && string.Equals(Focused(window)?.Name, "SheetTitle", StringComparison.Ordinal));
        Assert.Equal(opener, shell.SheetSubject);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Until(window, () => !shell.IsSheetShown && OnRowFrame(window, opener));
    });

    // a11y-2: the opener row cleared while the sheet was open: Esc focuses the row the selection moved to, not the page region.
    [Fact]
    public Task Esc_after_the_opener_row_cleared_returns_focus_to_the_row_the_selection_moved_to() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1000);
        shell.GoTo(PageId.Work);
        Settle(window);
        window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        await Until(window, () => shell.Queue.SelectedId is not null && FocusedRowId(window) == shell.Queue.SelectedId);
        var opener = shell.Queue.SelectedId!;
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => shell.IsSheetShown && string.Equals(Focused(window)?.Name, "SheetTitle", StringComparison.Ordinal));

        const string successor = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a3ff";
        _server.QueueView = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("queue", "fixture").Replace(opener, successor, StringComparison.Ordinal));
        _server.Push(A.Event(58, "item.state-changed"));
        await Until(window, () => shell.Queue.SelectedId == successor);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Until(window, () => !shell.IsSheetShown && OnRowFrame(window, successor), "Esc did not focus the successor row");
    });

    // L5: beside the page (wide window) the same: Esc from inside the sheet returns to the opening row.
    [Fact]
    public Task Esc_from_the_beside_sheet_returns_focus_to_the_row_that_opened_it() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1400);
        shell.GoTo(PageId.Work);
        Settle(window);
        window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        await Until(window, () => shell.Queue.SelectedId is not null && FocusedRowId(window) == shell.Queue.SelectedId);
        var opener = shell.Queue.SelectedId!;
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => shell.IsSheetShown);
        await Until(window, () => shell.Item.HasItem || shell.Item.IsNotFound);

        Assert.True(Named<Button>(window, "SheetClose").Focus(NavigationMethod.Tab));
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Until(window, () => !shell.IsSheetShown && OnRowFrame(window, opener));
    });

    // R6: a row asked for focus is gone by the time the request runs (its group is hidden by a filter pressed in between): the
    // request is not lost with it, whoever had focus still gets its neighbour.
    [Fact]
    public Task A_focus_request_for_a_row_that_is_no_longer_drawn_still_keeps_focus_on_the_page() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Work);
        Settle(window);
        var title = Named<Button>(Rows(window)[^1], "TitleButton");
        Assert.True(title.Focus(NavigationMethod.Tab));
        shell.Queue.Move(1, QueueScope.Page);   // asks for focus on a row; the request runs at the next Loaded job

        shell.GoToQueue(QueueFilter.Running);   // hides every group that has a row: the focused one and the asked-for one
        Settle(window);

        var focused = Focused(window);
        Assert.NotNull(focused);
        Assert.True(focused.IsEffectivelyVisible && focused.IsEffectivelyEnabled);
        Assert.NotSame(title, focused);
    });
}
