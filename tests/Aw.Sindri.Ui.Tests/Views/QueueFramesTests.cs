using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// U2's key frames (UI spec §18.2: one per state in the integration lane), against the in-memory API serving the
/// spec-shaped P3 fixtures: the Queue page populated, empty and loading; Home's queue panels; the item sheet with its
/// timeline; SND-ITEM-404 and SND-ITEM-409; the Create item form; the Close confirm. Saved under frames/ as u2-*.png.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class QueueFramesTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(QueueFramesTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected(Func<ShellViewModel, bool> ready)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && !(shell.State == ShellState.Normal && ready(shell)); i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(ready(shell), "the shell never reached the expected state");
        Settle(window);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static bool Visible(Visual within, string name) => within.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name)?.IsEffectivelyVisible == true;

    private static void Frame(Window window, string name)
    {
        Settle(window);
        FrameFile.Save(window, "u2-" + name);
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

    private static async Task<(ShellWindow, ShellViewModel)> OpenItem(Task<(ShellWindow Window, ShellViewModel Shell)> connected, string itemId)
    {
        var (window, shell) = await connected;
        shell.OpenItem(itemId);
        await Until(window, () => shell.Item.HasItem || shell.Item.IsNotFound);
        Settle(window);
        Assert.True(Visible(window, "ItemSheet"));
        return (window, shell);
    }

    [Fact]
    public Task The_queue_page_shows_the_apis_groups_and_rows() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => !s.Queue.IsLoading);
        shell.GoTo(PageId.Work);
        Settle(window);

        var page = Named<Control>(window, "QueuePage");
        var headers = page.GetVisualDescendants().OfType<ToggleButton>().Where(t => t.Classes.Contains("group-head") && t.IsEffectivelyVisible && t.Name != "QueueClosedToggle").Select(AutomationProperties.GetName).ToList();
        Assert.Equal(["Queued 4"], headers, StringComparer.Ordinal);
        var rows = page.GetVisualDescendants().OfType<Ui.Views.Queue.ItemRow>().Where(r => r.IsEffectivelyVisible).Select(r => r.ItemId).ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal(A.NewItem, rows[^1]);
        Assert.Equal("4", shell.Navigation[2].Count);
        Frame(window, "queue-populated");
    });

    [Fact]
    public Task An_empty_queue_shows_the_views_sentence() => OnUi(async () =>
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "empty");
        var (window, shell) = await Connected(s => !s.Queue.IsLoading);
        shell.GoTo(PageId.Work);
        Settle(window);

        var empty = Named<TextBlock>(window, "QueueEmpty");
        Assert.Equal((true, "Nothing in the queue. Add one with `sindri item add`."), (empty.IsEffectivelyVisible, empty.Text));
        Assert.Equal("Nothing running.", Named<TextBlock>(window, "RunningEmpty").Text);
        Frame(window, "queue-empty");
    });

    // §4.1: skeleton rows on the first load only.
    [Fact]
    public Task The_queue_is_skeleton_rows_until_its_first_answer() => OnUi(async () =>
    {
        _server.QueueGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (window, shell) = await Connected(s => s.State == ShellState.Normal);
        shell.GoTo(PageId.Work);
        Settle(window);

        Assert.True(Visible(window, "QueueSkeleton"));
        Assert.False(Visible(window, "QueueEmpty"));
        Frame(window, "queue-loading");
    });

    // F3 spec work §1 Q2: the title leads each row; the id is a small grey string beside it, never a column or a header.
    [Fact]
    public Task The_queue_leads_with_the_title_and_draws_the_id_beside_it_not_as_a_column() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => !s.Queue.IsLoading);
        shell.GoTo(PageId.Work);
        Settle(window);

        var page = Named<Control>(window, "QueuePage");
        Assert.Contains(page.GetVisualDescendants().OfType<Control>(), c => c.Name == "IdText");
        var header = page.GetVisualDescendants().OfType<Grid>().First(g => g.Name == "QueueHeader");
        Assert.Equal(["Title", "Repo", "Status", "Stage", "Age"], header.Children.OfType<TextBlock>().Select(t => t.Text), StringComparer.Ordinal);
        Assert.Equal(0, Grid.GetColumn(header.Children.OfType<TextBlock>().First()));
    });

    // §3.2: Running and With you (With you first, at most five, then Show all) beside Queued.
    [Fact]
    public Task Homes_queue_panels_cap_their_rows() => OnUi(async () =>
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var (window, _) = await Connected(s => !s.Queue.IsLoading);

        var running = Named<Control>(window, "RunningRows").GetVisualDescendants().OfType<Ui.Views.Queue.CompactItemRow>().ToList();
        Assert.Equal(5, running.Count);
        Assert.Equal("Show all 7 in Queue", AutomationProperties.GetName(Named<Button>(window, "RunningShowAll")));
        Assert.True(Visible(window, "RunningShowAll"));
        Frame(window, "home-panels");
    });

    [Fact]
    public Task The_item_sheet_shows_the_item_its_actions_and_its_timeline() => OnUi(async () =>
    {
        var (window, shell) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);

        Assert.Equal("Try the queue", Named<TextBlock>(window, "ItemTitle").Text);
        Assert.False(Visible(window, "ItemIdText"));   // 456-3: the raw id waits behind Details too, with Copy
        Assert.False(Visible(window, "ItemVersion"));   // F3: who made it, the version and the run's address wait behind Details
        shell.Item.DetailsOpen = true;
        await Until(window, () => Visible(window, "ItemVersion"));
        Assert.Equal("1", Named<TextBlock>(window, "ItemVersion").Text);
        Assert.Equal(A.NewItem, Named<TextBlock>(window, "ItemIdText").Text);
        Assert.True(Visible(window, "ItemCopyId"));
        Assert.Equal("New for 5m", Named<TextBlock>(window, "ItemStatusLine").Text);
        var actions = Named<Control>(window, "ItemActionList").GetVisualDescendants().OfType<Button>().Where(b => b.Name == "RunButton").Select(AutomationProperties.GetName).ToList();
        Assert.Equal(["Close item " + A.NewItem, "Close as cancelled " + A.NewItem], actions, StringComparer.Ordinal);
        Assert.True(Visible(window, "LoadMoreButton"));
        Frame(window, "item-sheet");

        Named<Button>(window, "LoadMoreButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => !shell.Item.HasMore);
        Assert.Equal(101, Named<Control>(window, "TimelineList").GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("timeline-row")));
        Frame(window, "item-timeline");
    });

    // §8 E-8: Close as cancelled asks first; Keep is focused; Esc keeps.
    [Fact]
    public Task Close_as_cancelled_asks_before_it_sends() => OnUi(async () =>
    {
        var (window, shell) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);
        var cancel = Named<Control>(window, "ItemActionList").GetVisualDescendants().OfType<Ui.Views.Controls.CommandControl>().ElementAt(1);

        Named<Button>(cancel, "RunButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.True(Visible(cancel, "ConfirmRow"));
        Assert.Equal("Close as cancelled " + A.NewItem + "?", Named<TextBlock>(cancel, "ConfirmText").Text);
        Assert.Empty(_server.Closed);
        Frame(window, "close-confirm");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);
        Assert.False(Visible(cancel, "ConfirmRow"));
        Assert.True(shell.IsSheetOpen);
    });

    // L11: the buttons carry the question and the item in their names (the question text itself is not read twice), and Esc returns to the control.
    [Fact]
    public Task The_confirm_row_names_its_buttons_and_escape_returns_focus_to_the_control() => OnUi(async () =>
    {
        var (window, _) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);
        var cancel = Named<Control>(window, "ItemActionList").GetVisualDescendants().OfType<Ui.Views.Controls.CommandControl>().ElementAt(1);
        Named<Button>(cancel, "RunButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.Equal("Confirm: Close as cancelled " + A.NewItem + "?", AutomationProperties.GetName(Named<Button>(cancel, "ConfirmButton")));
        Assert.Equal("Keep " + A.NewItem, AutomationProperties.GetName(Named<Button>(cancel, "KeepButton")));
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetName(Named<TextBlock>(cancel, "ConfirmText"))));

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);

        Assert.Same(Named<Button>(cancel, "RunButton"), Focused(window));
    });

    // a11y-4: focus put on a control by the app shows the keyboard's ring (:focus-visible), both on Keep and back on the control.
    [Fact]
    public Task Focus_the_app_puts_on_a_command_control_shows_the_keyboard_ring() => OnUi(async () =>
    {
        var (window, _) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);
        var cancel = Named<Control>(window, "ItemActionList").GetVisualDescendants().OfType<Ui.Views.Controls.CommandControl>().ElementAt(1);
        Named<Button>(cancel, "RunButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => string.Equals((Focused(window) as Control)?.Name, "KeepButton", StringComparison.Ordinal));
        Assert.True((Focused(window) as Control)!.Classes.Contains(":focus-visible"), "Keep has focus without the ring");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        var run = Named<Button>(cancel, "RunButton");
        await Until(window, () => ReferenceEquals(Focused(window), run));

        Assert.True(run.Classes.Contains(":focus-visible"), "the run button has focus without the ring");
    });

    // L12: Load more is named for the timeline it extends; when the page lands, focus goes to its first new row.
    [Fact]
    public Task Load_more_is_named_for_its_timeline_and_focus_moves_to_the_first_new_row() => OnUi(async () =>
    {
        var (window, shell) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);
        var more = Named<Button>(window, "LoadMoreButton");
        Assert.Equal("Load more timeline for " + A.NewItem, AutomationProperties.GetName(more));
        Assert.True(more.Focus(NavigationMethod.Tab));

        more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => !shell.Item.HasMore);

        var rows = Named<Control>(window, "TimelineList").GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("timeline-row")).ToList();
        Assert.Same(rows[100], Focused(window));
    });

    // a11y-7: the item vanishes (404 on a refresh) while focus is inside its body: focus goes to the sheet's title, not to nothing.
    [Fact]
    public Task A_404_on_refresh_moves_focus_from_the_body_to_the_sheet_title() => OnUi(async () =>
    {
        var (window, shell) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);
        var row = Named<Control>(window, "TimelineList").GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("timeline-row"));
        Assert.True(row.Focus(NavigationMethod.Tab));

        _ = _server.Items.Remove(A.NewItem);
        _server.Push(A.Event(58, "item.state-changed"));
        await Until(window, () => shell.Item.IsNotFound);

        await Until(window, () => string.Equals((Focused(window) as Control)?.Name, "SheetTitle", StringComparison.Ordinal));
    });

    // a11y-6: an empty last page hides the button the human pressed: focus goes to the last timeline row, never to nothing.
    [Fact]
    public Task An_empty_last_page_leaves_focus_on_the_last_timeline_row() => OnUi(async () =>
    {
        _server.TimelinePages[151] = new RouteAnswer(System.Net.HttpStatusCode.OK, """{"entries":[],"next":null,"asOf":{"cursor":57,"ledgerHead":57,"projectedAt":null,"rebuilding":false,"stalled":false}}""");
        var (window, shell) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);
        var more = Named<Button>(window, "LoadMoreButton");
        Assert.True(more.Focus(NavigationMethod.Tab));

        more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => !shell.Item.HasMore);

        var rows = Named<Control>(window, "TimelineList").GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("timeline-row")).ToList();
        await Until(window, () => ReferenceEquals(rows[^1], Focused(window)));
    });

    // §18 U2's signal: Close with a stale version shows SND-ITEM-409's detail as served.
    [Fact]
    public Task A_stale_close_shows_SND_ITEM_409s_detail() => OnUi(async () =>
    {
        var (window, shell) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");

        var close = Named<Control>(window, "ItemActionList").GetVisualDescendants().OfType<Button>().First(b => b.Name == "RunButton");
        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Item.HasProblem);

        Assert.Equal(("SND-ITEM-409", A.Detail("items-id-close", "stale-version")), (Named<TextBlock>(window, "ProblemCodeText").Text, Named<TextBlock>(window, "ProblemDetailText").Text));
        Assert.StartsWith("Done", Named<TextBlock>(window, "ItemStatusLine").Text, StringComparison.Ordinal);
        Frame(window, "item-409");
    });

    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    private static Button FirstRunButton(Window window) =>
        Named<Control>(window, "ItemActionList").GetVisualDescendants().OfType<Button>().First(b => b.Name == "RunButton");

    // L8: the control the human pressed is gone with its action; focus moves to the sheet title and is never lost.
    [Fact]
    public Task When_a_refused_controls_action_is_gone_focus_moves_to_the_sheet_title() => OnUi(async () =>
    {
        var (window, shell) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        var close = FirstRunButton(window);
        Assert.True(close.Focus(NavigationMethod.Tab));

        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Item.HasProblem && shell.Item.Actions.Count == 0);

        Assert.Same(Named<Control>(window, "SheetTitle"), Focused(window));
    });

    // L8: the accepted control stays while it shows its outcome, with focus; when it settles, focus moves to the title.
    [Fact]
    public Task An_accepted_control_keeps_focus_until_it_settles_then_focus_moves_to_the_sheet_title() => OnUi(async () =>
    {
        var (window, shell) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);
        _server.OnClosed = (_, _) => _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        var close = FirstRunButton(window);
        Assert.True(close.Focus(NavigationMethod.Tab));

        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        _server.Push(A.Event(58, "item.state-changed"));
        await Until(window, () => shell.Item.StateWord == "Done");

        Assert.Same(close, Focused(window));
        Assert.Equal(1, shell.Item.Actions.Count(a => a.Phase == CommandPhase.Accepted));

        _clock.Advance(ControlViewModel.AcceptedFor);
        Settle(window);

        Assert.Same(Named<Control>(window, "SheetTitle"), Focused(window));
    });

    [Fact]
    public Task An_unknown_item_shows_SND_ITEM_404s_detail() => OnUi(async () =>
    {
        var (window, _) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.MissingItem);

        Assert.Equal(("SND-ITEM-404", A.Detail("items-id", "not-found")), (Named<TextBlock>(window, "ProblemCodeText").Text, Named<TextBlock>(window, "ProblemDetailText").Text));
        Assert.False(Visible(window, "ItemBody"));
        Frame(window, "item-404");
    });

    // §4.5: C opens the form with focus in the title; it sends through the catalog's control.
    [Fact]
    public Task C_opens_the_create_form_and_it_sends_the_title() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => !s.Queue.IsLoading);
        _ = Named<Control>(window, "Page").Focus();

        window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.None);
        Settle(window);
        Assert.True(Visible(window, "CreateLayer"));
        Assert.True(Named<TextBox>(window, "TitleField").IsFocused);

        window.KeyTextInput("Proof: appears live");
        Settle(window);
        Frame(window, "create");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => _server.Created.Count == 1);
        Assert.Equal("Proof: appears live", _server.Created[0].Title);
        Assert.Equal("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a306", shell.Create.CreatedId);
    });

    // §4.5 (owner, 2026-10-12): a parked item shows in Waiting on you with a "paused" badge, from its state.
    [Fact]
    public Task A_parked_item_shows_its_badge_in_waiting_on_you() => OnUi(async () =>
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-parked");
        var (window, shell) = await Connected(s => !s.Queue.IsLoading);
        shell.GoTo(PageId.Work);
        Settle(window);

        var badges = Named<Control>(window, "QueuePage").GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Name == "BadgeText" && t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Equal(["paused"], badges, StringComparer.Ordinal);
        Frame(window, "queue-parked");
    });

    // §4.6 (owner, 2026-10-12): the sheet shows the item's body and link.
    [Fact]
    public Task The_item_sheet_shows_the_body_and_link() => OnUi(async () =>
    {
        var (window, _) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);

        Assert.Equal("Check the queue shows a new item without a click.", Named<TextBlock>(window, "ItemBodyText").Text);
        Assert.Equal("linear:issue/ENG-142", Named<TextBlock>(window, "ItemLinkText").Text);
        Frame(window, "item-body");
    });

    // L7 (corr-7): a failed first read shows the code and detail on the page; a later failed read marks what is shown stale.
    [Fact]
    public Task A_failed_first_queue_read_shows_its_problem_and_a_later_failure_marks_the_panels_stale() => OnUi(async () =>
    {
        _server.QueueView = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)");
        var (window, shell) = await Connected(s => !s.Queue.IsLoading);
        shell.GoTo(PageId.Work);
        Settle(window);
        Assert.Equal((true, "SND-API-011"), (Visible(window, "QueueProblem"), Named<TextBlock>(window, "QueueProblemCode").Text));
        Assert.False(Visible(window, "QueueSkeleton"));

        _server.QueueView = RouteAnswer.SpecShaped("queue", "fixture");
        await shell.Queue.Refresh(new Uri("http://127.0.0.1:7410/"), Token);
        _server.QueueView = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "behind");
        await shell.Queue.Refresh(new Uri("http://127.0.0.1:7410/"), Token);
        Settle(window);
        Assert.Equal((false, true), (Visible(window, "QueueProblem"), Visible(window, "QueueStale")));

        shell.GoTo(PageId.Home);
        Settle(window);
        Assert.Equal((true, true), (Visible(window, "RunningStale"), Visible(window, "QueuedStale")));
    });

    [Fact]
    public Task A_stale_item_read_marks_the_sheet_stale() => OnUi(async () =>
    {
        var (window, shell) = await OpenItem(Connected(s => !s.Queue.IsLoading), A.NewItem);
        Assert.False(Visible(window, "ItemStale"));

        _server.Items[A.NewItem] = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "behind");
        await shell.Item.Refresh(new Uri("http://127.0.0.1:7410/"), Token);
        Settle(window);

        Assert.True(Visible(window, "ItemStale"));
    });
}
