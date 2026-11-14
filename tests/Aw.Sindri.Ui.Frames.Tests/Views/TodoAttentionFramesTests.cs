using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Todo;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The attention rows of the Todo panel on Home under the headless platform (todo spec §3.1, §3.3, §3.4, D11, D13): one frame test per state, from the folded line to the pick's countdown toast.</summary>
[Lane(TestLane.Integration)]
public sealed class TodoAttentionFramesTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);   // Sunday 2026-10-11

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TodoAttentionFramesTests).Assembly)
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

    private static JsonNode Todo(string id, string text)
    {
        var todo = JsonNode.Parse(TodoSample.List)!["todos"]![3]!.DeepClone();   // "call the dentist": open, no date, no links
        todo["id"] = id;
        todo["text"] = text;
        todo["due"] = null;
        todo["addedBy"] = "human:ui";
        todo["lastBy"] = "human:ui";
        return todo;
    }

    private static JsonObject Side(string? text, string? due = "2026-10-12", bool done = false, bool removed = false) =>
        new() { ["text"] = text, ["due"] = due, ["links"] = new JsonArray(), ["done"] = done, ["removed"] = removed };

    private static JsonNode Conflicted(string id, string sindri, JsonNode obsidianSide)
    {
        var todo = Todo(id, sindri);
        todo["conflict"] = new JsonObject { ["sindri"] = Side(sindri), ["obsidian"] = obsidianSide, ["noteHash"] = "sha256:ab" };
        return todo;
    }

    private static JsonNode[] Six() => [.. Enumerable.Range(0, 6).Select(i => Todo("td_open" + i.ToString(CultureInfo.InvariantCulture), "open " + i.ToString(CultureInfo.InvariantCulture)))];

    private void Serve(string note, params JsonNode[] first)
    {
        var list = JsonNode.Parse(TodoSample.List)!;   // why: the sample is a JSON document
        list["todos"] = new JsonArray([.. first, .. Six()]);
        _server.TodosAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
        _server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, note);
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = 1000, Height = 800 };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0 && !shell.Todo.IsLoading);
        _clock.Advance(TimeSpan.FromSeconds(1));   // past the quiet window of a row that came with the first read
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
        for (var i = 0; i < 3000 && !condition(); i++)
        {
            await Task.Delay(1, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(condition(), what);
    }

    private static Control Panel(Window window) => window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "TodoPanelFrame");

    private static bool Shown(Window window, string name) => Panel(window).GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible);

    private static T Named<T>(Window window, string name)
        where T : Control => Panel(window).GetVisualDescendants().OfType<T>().First(c => c.Name == name && c.IsEffectivelyVisible);

    private static string[] Texts(Window window, string name) =>
        [.. Panel(window).GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == name && t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    private static List<Button> Buttons(Window window) =>
        [.. Panel(window).GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("attention-button") && b.IsEffectivelyVisible)];

    private static int TodoRowCount(Window window) =>
        Panel(window).GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("todo-row") && b.IsEffectivelyVisible);

    private static void Key(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Settle(window);
    }

    private static void FocusAttention(Window window)
    {
        _ = Named<Border>(window, "TodoAttentionLine").Focus(NavigationMethod.Tab);
        Settle(window);
    }

    private static string Focused(Window window) => window.FocusManager?.GetFocusedElement() is Control c ? c.Name ?? c.GetType().Name : "(nothing)";

    [Fact]
    public Task One_conflict_draws_one_line_with_the_Enter_hint_and_four_todo_rows() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithConflicts("td_c1"), Conflicted("td_c1", "reply to Sam", Side("reply to Sam today")));
        var (window, _) = await Connected();

        Assert.Equal(["Changed in Sindri and in Obsidian: reply to Sam"], Texts(window, "TodoAttentionLineText"));
        Assert.Equal(["to sort it out"], Texts(window, "TodoAttentionHint"));
        Assert.False(Shown(window, "TodoAttentionForm"));
        Assert.False(Shown(window, "TodoAttentionMore"));
        Assert.Equal(4, TodoRowCount(window));
    });

    [Fact]
    public Task Enter_opens_the_row_to_two_columns_with_the_changed_marks_and_focuses_the_first_button_and_Esc_folds_it() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithConflicts("td_c1"), Conflicted("td_c1", "reply to Sam", Side("reply to Sam today", "2026-10-14")));
        var (window, shell) = await Connected();
        FocusAttention(window);

        Key(window, PhysicalKey.Enter);

        Assert.True(shell.Todo.IsAttentionOpen);
        Assert.Equal(["Changed in Sindri and in Obsidian"], Texts(window, "TodoAttentionTitle"));
        Assert.Equal(["Sindri", "Obsidian"], Texts(window, "TodoAttentionHeading"));
        Assert.Equal(["reply to Sam", "Mon 10-12", "reply to Sam today", "Wed 10-14"], Texts(window, "TodoAttentionFieldValue"));
        Assert.Equal(["changed", "changed", "changed", "changed"], Texts(window, "TodoAttentionMark"));
        Assert.Equal(["Keep Sindri's", "Keep Obsidian's"], Buttons(window).Select(b => ((AttentionChoiceViewModel)b.DataContext!).ButtonText), StringComparer.Ordinal);
        Assert.Equal(["1", "2"], Texts(window, "TodoAttentionKey"));
        await Until(window, () => Buttons(window).Count == 2 && ReferenceEquals(Buttons(window)[0], window.FocusManager!.GetFocusedElement()), "focus never reached the first button");

        Key(window, PhysicalKey.Escape);

        Assert.False(shell.Todo.IsAttentionOpen);
        await Until(window, () => string.Equals(Focused(window), "TodoAttentionLine", StringComparison.Ordinal), "focus never came back to the line");
    });

    [Fact]
    public Task Three_conflicts_fold_into_Sort_out_2_more_and_Enter_on_it_opens_the_next() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithConflicts("td_c1", "td_c2", "td_c3"), Conflicted("td_c1", "first", Side("first!")), Conflicted("td_c2", "second", Side("second!")), Conflicted("td_c3", "third", Side("third!")));
        var (window, shell) = await Connected();

        Assert.Equal(["Changed in Sindri and in Obsidian: first"], Texts(window, "TodoAttentionLineText"));
        Assert.Equal(["Sort out 2 more"], Texts(window, "TodoAttentionMoreText"));
        Assert.Equal(3, TodoRowCount(window));
        _ = Named<Border>(window, "TodoAttentionMore").Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.Enter);

        Assert.Equal(("td_c2", false), (shell.Todo.Attention!.Id, shell.Todo.IsAttentionOpen));
        Assert.Equal(["Changed in Sindri and in Obsidian: second"], Texts(window, "TodoAttentionLineText"));
    });

    [Fact]
    public Task A_conflict_and_a_hold_draw_the_conflict_line_and_the_more_line_for_the_hold() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithHold(14, "sha256:3f1c", "td_c1"), Conflicted("td_c1", "reply to Sam", Side("reply to Sam today")));
        var (window, _) = await Connected();

        Assert.Equal(["Changed in Sindri and in Obsidian: reply to Sam"], Texts(window, "TodoAttentionLineText"));
        Assert.Equal(["Sort out 1 more"], Texts(window, "TodoAttentionMoreText"));
        Assert.Equal(3, TodoRowCount(window));
    });

    [Fact]
    public Task The_hold_row_opens_with_Put_them_back_on_1_and_Apply_on_2_and_its_changes() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithHold(14));
        var (window, shell) = await Connected();
        Assert.Equal(["The note changed 14 todos."], Texts(window, "TodoAttentionLineText"));
        Assert.Equal(["to look"], Texts(window, "TodoAttentionHint"));
        FocusAttention(window);

        Key(window, PhysicalKey.Enter);

        Assert.True(shell.Todo.IsAttentionOpen);
        Assert.Equal(["Put them back in the note", "Apply in Sindri too"], Buttons(window).Select(b => ((AttentionChoiceViewModel)b.DataContext!).ButtonText), StringComparer.Ordinal);
        Assert.Equal(["1", "2"], Texts(window, "TodoAttentionKey"));
        Assert.Equal(5, Texts(window, "TodoAttentionChange").Length);
        Assert.Equal(["and 9 more"], Texts(window, "TodoAttentionMoreChanges"));
    });

    [Fact]
    public Task A_280_character_text_wraps_and_clamps_to_three_lines_in_its_column() => OnUi(async () =>
    {
        var text = string.Join(' ', Enumerable.Repeat("reply", 56));   // 56 x "reply" and 55 spaces = 335; cut to 280 below
        text = text[..280];
        Serve(TodoNoteSample.WithConflicts("td_c1"), Conflicted("td_c1", text, Side(text + "!")));
        var (window, _) = await Connected();
        FocusAttention(window);
        Key(window, PhysicalKey.Enter);

        var values = Panel(window).GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "TodoAttentionFieldValue" && t.IsEffectivelyVisible).ToList();

        Assert.All(values, v => Assert.Equal((TextWrapping.Wrap, 3, TextTrimming.CharacterEllipsis), (v.TextWrapping, v.MaxLines, v.TextTrimming)));
        Assert.All(values, v => Assert.True(v.Bounds.Height <= (v.FontSize * 2 * 3) + 4, "a version's text is at most three lines tall"));
    });

    [Fact]
    public Task A_removal_draws_Removed_in_its_column_and_Remove_it_on_its_button() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithConflicts("td_c1"), Conflicted("td_c1", "reply to Sam", Side(text: null, due: null, removed: true)));
        var (window, _) = await Connected();
        FocusAttention(window);

        Key(window, PhysicalKey.Enter);

        Assert.Contains("Removed", Texts(window, "TodoAttentionFieldValue"), StringComparer.Ordinal);
        Assert.Equal(["Keep Sindri's", "Remove it"], Buttons(window).Select(b => ((AttentionChoiceViewModel)b.DataContext!).ButtonText), StringComparer.Ordinal);
    });

    [Fact]
    public Task Pressing_2_sends_keep_obsidian_drops_the_row_and_shows_the_counting_toast_and_Undo_sends_the_undo() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithConflicts("td_c1"), Conflicted("td_c1", "reply to Sam", Side("reply to Sam today")));
        var (window, shell) = await Connected();
        FocusAttention(window);

        Key(window, PhysicalKey.Digit2);
        await Until(window, () => shell.Toast.Text == "Kept Obsidian's version.");

        Assert.Equal(["/sindri/todos/td_c1/keep"], _server.TodoPicks.Select(p => p.Route), StringComparer.Ordinal);
        Assert.False(Shown(window, "TodoAttentionLine"));
        Assert.Equal(("Undo (10 s)", true), (shell.Toast.UndoLabel, shell.Toast.IsPickOpen));

        Assert.True(await shell.Toast.Undo());
        Assert.Equal(["/sindri/todos/td_c1/keep", "/sindri/todos/td_c1/keep/undo"], _server.TodoPicks.Select(p => p.Route), StringComparer.Ordinal);
    });

    [Fact]
    public Task Pressing_1_on_a_hold_puts_the_lines_back_and_a_409_redraws_the_row_with_the_new_count() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithHold(14, "sha256:3f1c"));
        var (window, shell) = await Connected();
        FocusAttention(window);
        _server.TodoPickAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The note changed again: now 9. Check and pick again.");
        _server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSample.WithHold(9, "sha256:9999"));

        Key(window, PhysicalKey.Digit1);
        await Until(window, () => Texts(window, "TodoAttentionLineText").SequenceEqual(["The note changed 9 todos."]));

        Assert.Equal(["The note changed again: now 9. Check and pick again."], Texts(window, "TodoAttentionNotice"));
        Assert.False(shell.Toast.IsShown);
    });

    [Fact]
    public Task X_on_a_conflict_row_sends_nothing_and_the_row_says_pick_a_version_first() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithConflicts("td_c1"), Conflicted("td_c1", "reply to Sam", Side("reply to Sam today")));
        var (window, _) = await Connected();
        FocusAttention(window);

        Key(window, PhysicalKey.X);

        Assert.Equal(["Pick a version first"], Texts(window, "TodoAttentionNotice"));
        Assert.Empty(_server.TodoWrites);
        Assert.Empty(_server.TodoPicks);
    });

    [Fact]
    public Task An_older_Sindri_with_no_note_route_draws_no_attention_row() => OnUi(async () =>
    {
        Serve(TodoNoteSample.Quiet, Conflicted("td_c1", "reply to Sam", Side("reply to Sam today")));
        _server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.NotFound, string.Empty);
        var (window, _) = await Connected();

        Assert.False(Shown(window, "TodoAttentionLine"));
        Assert.Equal(5, TodoRowCount(window));
    });

    [Fact]
    public Task After_a_409_redraws_the_open_form_a_digit_still_picks() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithConflicts("td_c1"), Conflicted("td_c1", "reply to Sam", Side("reply to Sam today")));
        var (window, shell) = await Connected();
        FocusAttention(window);
        Key(window, PhysicalKey.Enter);
        await Until(window, () => Focused(window) != "(nothing)" && window.FocusManager?.GetFocusedElement() is Button, "focus never reached the form's button");
        Serve(TodoNoteSample.WithConflicts("td_c1"), Conflicted("td_c1", "reply to Sam", Side("reply to Sam by noon")));
        _server.TodoPickAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The note's line changed since.");

        Key(window, PhysicalKey.Digit2);
        await Until(window, () => Texts(window, "TodoAttentionFormNotice").Length == 1, "the open form never showed the line-moved notice");
        await Until(window, () => window.FocusManager?.GetFocusedElement() is Button, "focus was lost with the redrawn form");
        _server.TodoPickAnswer = null;
        _clock.Advance(TimeSpan.FromSeconds(1));
        Key(window, PhysicalKey.Digit2);
        await Until(window, () => shell.Toast.Text == "Kept Obsidian's version.");

        Assert.Equal(2, _server.TodoPicks.Count);
    });

    [Fact]
    public Task A_sync_that_changes_the_open_row_leaves_focus_in_the_quick_add_box_and_a_digit_types_there() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithConflicts("td_c1"), Conflicted("td_c1", "reply to Sam", Side("reply to Sam today")));
        var (window, shell) = await Connected();
        FocusAttention(window);
        Key(window, PhysicalKey.Enter);
        await Until(window, () => window.FocusManager?.GetFocusedElement() is Button, "focus never reached the form's button");
        Assert.True(shell.PressQuickAdd());
        Settle(window);
        var field = window.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "QuickAddField" && t.IsEffectivelyVisible);
        _ = field.Focus();
        Settle(window);
        Serve(TodoNoteSample.WithConflicts("td_c1"), Conflicted("td_c1", "reply to Sam", Side("reply to Sam by noon")));

        await shell.Todo.Refresh(new Uri("http://127.0.0.1:7410/"), Token);
        Settle(window);
        await Task.Delay(50, Token);
        Settle(window);
        _clock.Advance(TimeSpan.FromSeconds(1));
        window.KeyTextInput("2");
        Settle(window);

        Assert.Same(field, window.FocusManager?.GetFocusedElement());
        Assert.Contains("2", field.Text, StringComparison.Ordinal);
        Assert.Empty(_server.TodoPicks);
    });

    [Fact]
    public Task Esc_with_focus_outside_the_panel_leaves_the_open_attention_row_open() => OnUi(async () =>
    {
        Serve(TodoNoteSample.WithConflicts("td_c1"), Conflicted("td_c1", "reply to Sam", Side("reply to Sam today")));
        var (window, shell) = await Connected();
        FocusAttention(window);
        Key(window, PhysicalKey.Enter);
        Assert.True(shell.Todo.IsAttentionOpen);
        var outside = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "RowFrame" && b.Focusable && b.IsEffectivelyVisible);
        _ = outside.Focus(NavigationMethod.Tab);
        Settle(window);

        Key(window, PhysicalKey.Escape);

        Assert.True(shell.Todo.IsAttentionOpen);
    });
}
