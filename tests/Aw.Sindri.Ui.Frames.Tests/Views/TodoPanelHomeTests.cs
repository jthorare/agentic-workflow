using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
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
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Aw.Sindri.Ui.Views.Todo;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Todo panel on Home under the headless platform (todo spec §3.1, §3.4, §3.5): loading, empty, five rows, the grouped list, Show done, the filter, the region and a row's name.</summary>
[Lane(TestLane.Integration)]
public sealed class TodoPanelHomeTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);   // Sunday 2026-10-11

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TodoPanelHomeTests).Assembly)
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

    // The todos the server answers, written against the clock's day (2026-10-11).
    private void Serve(params JsonNode[] todos)
    {
        var list = JsonNode.Parse(TodoSample.List)!;   // why: the sample is a JSON document
        list["todos"] = new JsonArray(todos);
        _server.TodosAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
    }

    private static JsonNode Todo(string id, string text, string? due = null, int links = 0, string addedBy = "human:ui", string? doneAt = null, bool removed = false, string? removedAt = null)
    {
        var todo = JsonNode.Parse(TodoSample.List)!["todos"]![3]!.DeepClone();   // "call the dentist": open, no date, no links
        todo["id"] = id;
        todo["text"] = text;
        todo["due"] = due;
        todo["addedBy"] = addedBy;
        todo["lastBy"] = addedBy;
        todo["doneAt"] = doneAt;
        todo["removed"] = removed;
        todo["removedAt"] = removedAt;
        todo["links"] = new JsonArray([.. Enumerable.Range(0, links).Select(i => (JsonNode)new JsonObject
        {
            ["kind"] = "pr",
            ["ref"] = string.Create(CultureInfo.InvariantCulture, $"acme/web#{40 + i}"),
            ["href"] = string.Create(CultureInfo.InvariantCulture, $"sindri://reviews/acme/web/{40 + i}"),
        })]);
        return todo;
    }

    // Eight open todos: one overdue, two today, three upcoming, two with no date.
    private static JsonNode[] Eight() =>
    [
        Todo("td_1", "renew the staging cert", "2026-10-08"),
        Todo("td_2", "check the flaky test", "2026-10-11", links: 1),
        Todo("td_3", "rotate the staging key", "2026-10-11", addedBy: "session:mcp"),
        Todo("td_4", "prep the demo script", "2026-10-13", links: 5),
        Todo("td_5", "send the budget note", "2026-10-14"),
        Todo("td_6", "reply to Sam", "2026-10-16"),
        Todo("td_7", "call the dentist"),
        Todo("td_8", "book the room"),
    ];

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected(bool waitForTodos = true)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = 1440, Height = 900 };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        // The todo read is the first of Home's reads (D12): while a test holds it, the queue behind it hasn't loaded either.
        await Until(window, () => shell.State == ShellState.Normal && (!waitForTodos || (shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0 && !shell.Todo.IsLoading)));
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

    private static bool Shown(Visual within, string name) => within.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible);

    private static void Press(Window window, Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
    }

    private static Control Panel(Window window) => Named<Control>(window, "TodoPanelFrame");

    private static List<TodoRow> Rows(Window window) => [.. Panel(window).GetVisualDescendants().OfType<TodoRow>().Where(r => r.IsEffectivelyVisible)];

    private static string[] Texts(Visual within, string name) =>
        [.. within.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == name && t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    [Fact]
    public Task The_panel_shows_three_skeleton_rows_as_tall_as_loaded_rows_until_the_first_answer() => OnUi(async () =>
    {
        Serve(Eight());
        _server.TodosGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (window, shell) = await Connected(waitForTodos: false);

        var skeleton = Named<Control>(window, "TodoSkeleton");
        var bars = skeleton.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("item-row")).ToList();
        Assert.True(shell.Todo.IsLoading);
        Assert.True(skeleton.IsEffectivelyVisible);
        Assert.Equal(3, bars.Count);
        Assert.Empty(Rows(window));
        var skeletonHeight = bars[0].Bounds.Height;

        _server.TodosGate.SetResult();
        await Until(window, () => !shell.Todo.IsLoading);

        Assert.False(skeleton.IsEffectivelyVisible);
        Assert.Equal(skeletonHeight, Named<Control>(Rows(window)[0], "TodoRowFrame").Bounds.Height);
    });

    // The collapsed panel's height with the todos the server now answers (the skeleton when `hold`), on a fresh window.
    private async Task<double> CollapsedHeight(bool hold, params JsonNode[] todos)
    {
        Serve(todos);
        _server.TodosGate = hold ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        var (window, shell) = await Connected(waitForTodos: !hold);
        Assert.Equal(hold, shell.Todo.IsLoading);
        var height = Panel(window).Bounds.Height;
        _server.TodosGate?.SetResult();
        window.Close();
        return height;
    }

    [Fact]
    public Task The_collapsed_panel_keeps_one_height_whatever_todos_it_holds_and_an_empty_one_is_shorter() => OnUi(async () =>
    {
        var skeleton = await CollapsedHeight(hold: true, Eight());
        var empty = await CollapsedHeight(hold: false);
        var two = await CollapsedHeight(hold: false, Eight()[..2]);
        var six = await CollapsedHeight(hold: false, Eight()[..6]);

        Assert.True(skeleton > 0);
        Assert.Equal([skeleton, skeleton], [two, six]);
        Assert.True(empty > 0 && empty < skeleton, string.Create(CultureInfo.InvariantCulture, $"empty {empty} vs {skeleton}"));   // #2548: an empty list collapses to one line
    });

    // The collapsed body clips at five rows: a row drawn taller than its slot would hide the fifth.
    [Fact]
    public Task The_fifth_row_fits_inside_the_collapsed_body() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, _) = await Connected();

        var body = Named<Grid>(window, "TodoCollapsed");
        var fifth = Rows(window)[4];
        var bottom = fifth.TranslatePoint(new Point(0, fifth.Bounds.Height), body)!.Value.Y;   // why: both are in the same visual tree
        var slots = body.Bounds.Height;   // the whole body now

        Assert.True(bottom <= slots, string.Create(CultureInfo.InvariantCulture, $"the fifth row ends at {bottom}, the rows' part at {slots}"));
    });

    // #2546: the collapsed body is five Row.Height.Default slots, and a row is drawn exactly that tall (QueueStyles' item-row MinHeight).
    [Fact]
    public Task The_collapsed_body_is_five_rows_of_the_rows_own_height() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, _) = await Connected();

        var row = Assert.IsType<double>(window.FindResource("Row.Height.Default"));
        Assert.Equal(row * 5, Named<Control>(window, "TodoCollapsed").Bounds.Height);
        Assert.All(Rows(window).Take(5), r => Assert.Equal(row, r.Bounds.Height));
    });

    [Fact]
    public Task An_empty_list_says_so_and_offers_nothing_to_expand() => OnUi(async () =>
    {
        Serve();
        var (window, _) = await Connected();

        Assert.Equal([Labels.TodoEmpty], Texts(Panel(window), "TodoEmpty"));
        Assert.Empty(Rows(window));
        Assert.False(Shown(Panel(window), "TodoShowAll"));
        Assert.Equal("Nothing on your list. Press N to add one.", Labels.TodoEmpty);
    });

    [Fact]
    public Task The_header_has_the_title_the_count_line_and_the_new_todo_hint() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();

        Assert.Equal(["Todo"], Texts(Panel(window), "TodoTitle"));
        Assert.Equal(["8 open · 1 overdue"], Texts(Panel(window), "TodoCount"));
        Assert.Equal(shell.Todo.CountLine, Texts(Panel(window), "TodoCount")[0]);
        Assert.Equal(["N"], Texts(Panel(window), "TodoNewKey"));
        Assert.Equal(["New todo"], Texts(Panel(window), "TodoNewText"));
    });

    [Fact]
    public Task More_than_five_open_todos_show_five_rows_overdue_first_and_Show_all() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, _) = await Connected();

        var rows = Rows(window);
        Assert.Equal(5, rows.Count);
        Assert.Equal("renew the staging cert", Texts(rows[0], "TodoText")[0]);
        Assert.Equal(["Show all 8"], Texts(Panel(window), "TodoShowAllText"));
        Assert.True(Shown(Panel(window), "TodoShowAll"));
        Assert.False(Shown(Panel(window), "TodoDoneToggle"));
        Assert.False(Shown(Panel(window), "TodoShowLess"));
    });

    [Fact]
    public Task A_row_draws_the_checkbox_the_text_three_chips_then_plus_n_the_agent_tag_and_the_due_word() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, _) = await Connected();
        var rows = Rows(window);

        var overdue = rows[0];
        Assert.Equal(["Overdue · Thu"], Texts(overdue, "TodoDue"));
        var withChips = rows.Single(r => Texts(r, "TodoText")[0] == "prep the demo script");
        Assert.Equal(["acme/web#40", "acme/web#41", "acme/web#42"], Texts(withChips, "TodoChip"));
        Assert.All(withChips.GetVisualDescendants().OfType<TextBlock>().Where(t => string.Equals(t.Name, "TodoChip", StringComparison.Ordinal)), c => Assert.DoesNotContain(c.GetVisualAncestors().OfType<Border>(), b => b.Classes.Contains("phrase-chip")));   // muted text after the title, not boxed
        Assert.Equal(["+2"], Texts(withChips, "TodoMoreChips"));
        var agent = rows.Single(r => Texts(r, "TodoText")[0] == "rotate the staging key");
        Assert.Equal(["agent"], Texts(agent, "TodoTag"));
        Assert.Equal(["Today"], Texts(agent, "TodoDue"));
        Assert.Empty(Texts(overdue, "TodoTag"));
    });

    [Fact]
    public Task Show_all_expands_in_place_to_the_groups_with_their_own_scroll_the_panel_capped_at_half_the_window() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();

        Press(window, Named<Button>(window, "TodoShowAll"));

        Assert.True(shell.Todo.IsExpanded);
        Assert.Equal(["Overdue", "Today", "Upcoming", "No date"], Texts(Panel(window), "TodoGroupHeading"));
        Assert.Equal(8, Rows(window).Count);
        Assert.False(Shown(Panel(window), "TodoShowAll"));
        Assert.True(Shown(Panel(window), "TodoShowLess"));
        Assert.True(Shown(Panel(window), "TodoDoneToggle"));
        Assert.True(Panel(window).Bounds.Height <= window.ClientSize.Height / 2, "the whole panel (header, list and foot) is capped at half the page height");
    });

    [Fact]
    public Task Show_less_folds_back_to_five_rows() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();
        Press(window, Named<Button>(window, "TodoShowAll"));

        Press(window, Named<Button>(window, "TodoShowLess"));

        Assert.False(shell.Todo.IsExpanded);
        Assert.Equal(5, Rows(window).Count);
        Assert.Empty(Texts(Panel(window), "TodoGroupHeading"));
        Assert.True(Shown(Panel(window), "TodoShowAll"));
    });

    [Fact]
    public Task Show_done_adds_the_Done_and_Removed_groups_and_Hide_done_takes_them_away() => OnUi(async () =>
    {
        Serve([.. Eight(),
            Todo("td_d", "renew the cert", "2026-10-09", doneAt: "2026-10-10T09:00:00Z"),
            Todo("td_r", "book the offsite", removed: true, removedAt: "2026-10-11T08:00:00Z")]);
        var (window, shell) = await Connected();
        Press(window, Named<Button>(window, "TodoShowAll"));
        var toggle = Named<ToggleButton>(window, "TodoDoneToggle");
        Assert.Equal("Show done todos, 2", AutomationProperties.GetName(toggle));
        Assert.Equal(["Show done"], Texts(toggle, "TodoDoneToggleText"));

        Press(window, toggle);

        Assert.True(shell.Todo.ShowsDone);
        Assert.Equal(["Overdue", "Today", "Upcoming", "No date", "Done", "Removed"], Texts(Panel(window), "TodoGroupHeading"));
        Assert.Equal(10, Rows(window).Count);
        Assert.Equal(["Hide done"], Texts(toggle, "TodoDoneToggleText"));
        Assert.True(toggle.IsChecked);
        Assert.Empty(Texts(Panel(window), "TodoDoneEmpty"));

        Press(window, toggle);

        Assert.Equal(["Overdue", "Today", "Upcoming", "No date"], Texts(Panel(window), "TodoGroupHeading"));
    });

    [Fact]
    public Task Show_done_with_nothing_done_says_so_and_keeps_the_toggle() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, _) = await Connected();
        Press(window, Named<Button>(window, "TodoShowAll"));

        Press(window, Named<ToggleButton>(window, "TodoDoneToggle"));

        Assert.Equal(["Nothing done in the last 7 days"], Texts(Panel(window), "TodoDoneEmpty"));
        Assert.True(Shown(Panel(window), "TodoDoneToggle"));
        Assert.DoesNotContain("Done", Texts(Panel(window), "TodoGroupHeading"), StringComparer.Ordinal);
    });

    [Fact]
    public Task Folding_hides_the_Done_group_again() => OnUi(async () =>
    {
        Serve([.. Eight(), Todo("td_d", "renew the cert", doneAt: "2026-10-10T09:00:00Z")]);
        var (window, shell) = await Connected();
        Press(window, Named<Button>(window, "TodoShowAll"));
        Press(window, Named<ToggleButton>(window, "TodoDoneToggle"));
        Assert.Contains("Done", Texts(Panel(window), "TodoGroupHeading"), StringComparer.Ordinal);

        Press(window, Named<Button>(window, "TodoShowLess"));
        Press(window, Named<Button>(window, "TodoShowAll"));

        Assert.False(shell.Todo.ShowsDone);
        Assert.DoesNotContain("Done", Texts(Panel(window), "TodoGroupHeading"), StringComparer.Ordinal);
    });

    [Fact]
    public Task A_failed_read_shows_its_line_and_the_last_rows_stay() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();
        _server.TodosAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views are behind.");

        await shell.Todo.Refresh(new Uri("http://127.0.0.1:7410/"), Token);
        Settle(window);

        Assert.Equal([shell.Todo.Error], Texts(Panel(window), "TodoError"));
        Assert.Equal(5, Rows(window).Count);
    });

    // #2908: the read error draws once, in the header after the sync line; it never takes a body slot or the foot.
    private async Task FailRead(Window window, ShellViewModel shell, string detail)
    {
        _server.TodosAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", detail);
        await shell.Todo.Refresh(new Uri("http://127.0.0.1:7410/"), Token);
        Settle(window);
        Assert.False(string.IsNullOrEmpty(shell.Todo.Error));
    }

    [Fact]
    public Task A_read_error_collapsed_draws_in_the_header_and_leaves_the_five_slots_alone() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();
        var body = Named<Grid>(window, "TodoCollapsed");
        var elsewhere = Named<Control>(window, "TodoElsewhere");
        var before = (body.Bounds.Height, GapBelowBody(body, elsewhere));

        await FailRead(window, shell, "The views are behind.");

        var error = Named<TextBlock>(window, "TodoError");
        var sync = Named<TextBlock>(window, "TodoSyncLive");
        var head = Named<Control>(window, "TodoTitle").Parent!.Parent! as Visual ?? throw new InvalidOperationException("no header");   // the header grid's border
        Assert.True(error.IsEffectivelyVisible);
        Assert.True(error.TranslatePoint(default, head)!.Value.X >= sync.TranslatePoint(default, head)!.Value.X, "the error follows the sync line");
        Assert.True(error.TranslatePoint(new Point(0, error.Bounds.Height), Panel(window))!.Value.Y <= body.TranslatePoint(default, Panel(window))!.Value.Y, "the error is above the body");
        Assert.Equal(before.Height, body.Bounds.Height);
        Assert.Equal(Named<Control>(window, "TodoCollapsed").Bounds.Height, ((double)window.FindResource("Row.Height.Default")!) * 5);
        Assert.Equal(before.Item2, GapBelowBody(body, elsewhere));
        Assert.Equal(5, Rows(window).Count);
    });

    // The distance from the bottom of the five slots to the top of the From elsewhere row: 0 when it sits right under them.
    private static double GapBelowBody(Control body, Control elsewhere) =>
        elsewhere.TranslatePoint(default, body)!.Value.Y - body.Bounds.Height;

    [Fact]
    public Task A_read_error_expanded_shows_one_error_line() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();
        Press(window, Named<Button>(window, "TodoShowAll"));

        await FailRead(window, shell, "The views are behind.");

        var errors = Panel(window).GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && t.Text == shell.Todo.Error)
            .ToList();
        _ = Assert.Single(errors);
    });

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public Task A_long_read_error_trims_before_the_New_todo_button(double scale) => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();
        window.Width = 1000;
        TextScale.Apply(Application.Current!.Resources, scale);   // why: the headless session starts the app
        try
        {
            Settle(window);
            var hint = Named<Control>(window, "TodoNewHint");
            var hintWidth = hint.Bounds.Width;

            await FailRead(window, shell, string.Concat(Enumerable.Repeat("The views are behind. ", 10)));

            var error = Named<TextBlock>(window, "TodoError");
            var right = error.TranslatePoint(new Point(error.Bounds.Width, 0), window)!.Value.X;
            var hintLeft = hint.TranslatePoint(default, window)!.Value.X;
            Assert.True(error.IsEffectivelyVisible);
            Assert.True(error.Bounds.Width > 0, "the error has a width");
            var untrimmed = new TextBlock { Text = error.Text, FontSize = error.FontSize, FontFamily = error.FontFamily, FontWeight = error.FontWeight, FontStyle = error.FontStyle };
            untrimmed.Measure(Size.Infinity);
            Assert.True(untrimmed.DesiredSize.Width > error.Bounds.Width, string.Create(CultureInfo.InvariantCulture, $"the text is {untrimmed.DesiredSize.Width} wide untrimmed, the error draws {error.Bounds.Width}: it really trims"));
            Assert.True(right <= hintLeft, string.Create(CultureInfo.InvariantCulture, $"the error ends at {right}, New todo starts at {hintLeft}"));
            Assert.Equal(hintWidth, hint.Bounds.Width);
        }
        finally
        {
            TextScale.Apply(Application.Current!.Resources, 1.0);
        }
    });

    private static readonly string[] _syncBefore = ["TodoCount", "TodoSyncQuiet"];

    [Fact]
    public Task With_no_live_sync_text_the_error_sits_one_gap_after_the_last_visible_sync_element() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();
        await FailRead(window, shell, "The views are behind.");

        Assert.True(string.IsNullOrEmpty(shell.Todo.SyncLive), "no live sync text");
        var live = Named<TextBlock>(window, "TodoSyncLive");
        Assert.Equal(0, live.Bounds.Width);   // why: the empty live line takes no width and no margin
        var error = Named<TextBlock>(window, "TodoError");
        var lastRight = _syncBefore
            .Select(name => Named<TextBlock>(window, name))
            .Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0)
            .Max(t => t.TranslatePoint(new Point(t.Bounds.Width, 0), window)!.Value.X);
        var errorLeft = error.TranslatePoint(default, window)!.Value.X;
        Assert.Equal(12, errorLeft - lastRight, 0.5);
    });

    [Fact]
    public Task Sindri_not_running_shows_the_not_running_card_and_no_panel() => OnUi(async () =>
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = 1440, Height = 900 };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.NotRunning);

        Assert.True(Named<Control>(window, "NotRunningPanel").IsEffectivelyVisible);
        Assert.False(Shown(window, "TodoPanelFrame"));   // the card replaces all of Home, so the panel isn't drawn at all
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TodoRow>(), r => r.IsEffectivelyVisible);
    });

    [Fact]
    public Task Home_decisions_filter_hides_the_panel_and_Home_brings_it_back() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();
        Assert.True(Panel(window).IsEffectivelyVisible);

        shell.GoTo(PageId.Home, HomeFilter.Decisions);
        Settle(window);

        Assert.False(shell.IsTodoShown);
        Assert.False(Panel(window).IsEffectivelyVisible);

        shell.GoTo(PageId.Home, HomeFilter.All);
        Settle(window);

        Assert.True(shell.IsTodoShown);
        Assert.True(Panel(window).IsEffectivelyVisible);
    });

    [Fact]
    public Task The_panel_is_a_labelled_region_named_Todo_with_heading_led_groups() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, _) = await Connected();
        Press(window, Named<Button>(window, "TodoShowAll"));

        var panel = Panel(window);

        Assert.Equal("Todo", AutomationProperties.GetName(panel));
        Assert.Equal(AccessibilityView.Control, AutomationProperties.GetAccessibilityView(panel));
        var headings = panel.GetVisualDescendants().OfType<Control>().Where(c => c.Name == "TodoGroup" && c.IsEffectivelyVisible).ToList();
        Assert.Equal(4, headings.Count);
        Assert.All(headings, h => Assert.Equal(2, AutomationProperties.GetHeadingLevel(h)));
    });

    [Fact]
    public Task A_row_is_one_focus_stop_with_the_sentence_and_a_checkbox_named_Done() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();
        var row = Rows(window)[0];

        var frame = Named<Border>(row, "TodoRowFrame");
        Assert.Equal(shell.Todo.Rows[0].AccessibleName, AutomationProperties.GetName(frame));
        Assert.Equal("Todo, renew the staging cert, overdue, due Thursday, not done", AutomationProperties.GetName(frame));
        Assert.True(frame.Focusable);
        var box = Named<CheckBox>(row, "TodoDone");
        Assert.Equal("Done", AutomationProperties.GetName(box));
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(box));
        Assert.False(box.Focusable);   // why: the row is the one stop; check-off is a later slice
        Assert.Equal(["TodoRowFrame"], row.GetVisualDescendants().OfType<InputElement>().Where(c => c.Focusable && c.IsTabStop && c.IsEffectivelyVisible).Select(c => c.Name), StringComparer.Ordinal);
    });

    [Fact]
    public Task The_from_elsewhere_line_is_drawn_under_the_rows_inside_the_panel() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();
        await Until(window, () => !shell.Elsewhere.IsLoading && Shown(Panel(window), "ElsewhereFrame"));

        var line = Named<Control>(Panel(window), "ElsewhereFrame");
        var lastRow = Rows(window)[^1];
        var rowBottom = lastRow.TranslatePoint(new Point(0, lastRow.Bounds.Height), Panel(window))!.Value.Y;   // why: both are in the same visual tree
        var lineTop = line.TranslatePoint(default, Panel(window))!.Value.Y;

        Assert.True(lineTop >= rowBottom, string.Create(CultureInfo.InvariantCulture, $"the line starts at {lineTop}, the last row ends at {rowBottom}"));
        Assert.True(line.Bounds.Height > 0);
        Assert.True(line.TranslatePoint(new Point(0, line.Bounds.Height), Panel(window))!.Value.Y <= Panel(window).Bounds.Height);
    });

    // #2548: the Connect Linear link starts where the card's own text starts (the empty line's 16 px inset), not 6 px to its left.
    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task Connect_Linear_starts_at_the_left_edge_of_the_empty_line(int width) => OnUi(async () =>
    {
        Serve();
        _server.LinearIssuesAnswer = RouteAnswer.Api("linear-issues", "not-connected");
        var (window, shell) = await Connected();
        window.Width = width;
        await Until(window, () => !shell.Elsewhere.IsLoading && Shown(Panel(window), "ElsewhereFrame"));
        shell.Elsewhere.SetReviews(loading: false, waiting: 0, failed: false);   // why: with no PRs waiting, Connect Linear is the line's first part, as in the empty Home shot
        Settle(window);
        Assert.Equal([Labels.ElsewhereLinearNotConnected], Panel(window).GetVisualDescendants().OfType<Button>().Where(x => x.IsEffectivelyVisible && x.Classes.Contains("elsewhere-part")).Select(x => (x.Content as TextBlock)?.Text), StringComparer.Ordinal);

        var empty = Named<TextBlock>(Panel(window), "TodoEmpty");
        var link = Panel(window).GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && string.Equals(t.Text, Labels.ElsewhereLinearNotConnected, StringComparison.Ordinal));
        var emptyLeft = empty.TranslatePoint(default, Panel(window))!.Value.X;   // why: both are in the same visual tree
        var linkLeft = link.TranslatePoint(default, Panel(window))!.Value.X;

        Assert.True(Math.Abs(emptyLeft - linkLeft) < 0.5, string.Create(CultureInfo.InvariantCulture, $"at {width} px the empty line starts at {emptyLeft}, the Connect Linear link at {linkLeft}"));
    });

    // #2548: whichever part leads the From elsewhere line starts at the card's text inset, not only Connect Linear.
    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task The_first_elsewhere_part_starts_at_the_left_edge_of_the_empty_line(int width) => OnUi(async () =>
    {
        Serve();
        _server.LinearIssuesAnswer = RouteAnswer.Api("linear-issues", "not-connected");
        var (window, shell) = await Connected();
        window.Width = width;
        await Until(window, () => !shell.Elsewhere.IsLoading && Shown(Panel(window), "ElsewhereFrame"));
        shell.Elsewhere.SetReviews(loading: false, waiting: 2, failed: false);
        Settle(window);

        var empty = Named<TextBlock>(Panel(window), "TodoEmpty");
        var first = Panel(window).GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Classes.Contains("elsewhere-part"));
        var firstWords = first.GetVisualDescendants().OfType<TextBlock>().First();
        Assert.StartsWith(Labels.ElsewhereReviews(2), firstWords.Text, StringComparison.Ordinal);   // why: a missing Reviews part would otherwise measure Connect Linear
        var emptyLeft = empty.TranslatePoint(default, Panel(window))!.Value.X;   // why: both are in the same visual tree
        var firstLeft = firstWords.TranslatePoint(default, Panel(window))!.Value.X;

        Assert.True(Math.Abs(emptyLeft - firstLeft) < 0.5, string.Create(CultureInfo.InvariantCulture, $"at {width} px the empty line starts at {emptyLeft}, the first From elsewhere part ({firstWords.Text}) at {firstLeft}"));
    });

    [Fact]
    public Task The_panel_keeps_one_height_whatever_the_line_says() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();
        await Until(window, () => !shell.Elsewhere.IsLoading);
        var withParts = Panel(window).Bounds.Height;

        shell.Elsewhere.SetReviews(loading: false, waiting: 0, failed: false);
        Settle(window);
        var fewer = Panel(window).Bounds.Height;
        shell.Elsewhere.SetReviews(loading: true, waiting: 0, failed: false);
        Settle(window);

        Assert.Equal([withParts, withParts], [fewer, Panel(window).Bounds.Height]);
    });

    [Fact]
    public Task G_T_focuses_the_first_todo_row() => OnUi(async () =>
    {
        Serve(Eight());
        var (window, shell) = await Connected();

        shell.OpenTodo();
        await Until(window, () => window.FocusManager?.GetFocusedElement() is Border b && string.Equals(b.Name, "TodoRowFrame", StringComparison.Ordinal));

        var focused = (Visual)window.FocusManager!.GetFocusedElement()!;
        Assert.Same(Rows(window)[0], focused.GetSelfAndVisualAncestors().OfType<TodoRow>().First());
    });

    [Fact]
    public Task G_T_with_no_rows_focuses_the_panel_region() => OnUi(async () =>
    {
        Serve();
        var (window, shell) = await Connected();

        shell.OpenTodo();
        await Until(window, () => ReferenceEquals(window.FocusManager?.GetFocusedElement(), Panel(window)));

        Assert.Empty(Rows(window));
    });
}
