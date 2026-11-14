using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
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
using Aw.Sindri.Ui.ViewModels.Todo;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Todo panel's keys on Home under the headless platform (todo spec §3.3, §3.4, D6): the two focus regions, J and K, X, Space and Delete on a row, the toast's Undo and Esc.</summary>
[Lane(TestLane.Integration)]
public sealed class TodoPanelKeysTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);   // Sunday 2026-10-11

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TodoPanelKeysTests).Assembly)
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

    private void Serve(params JsonNode[] todos)
    {
        var list = JsonNode.Parse(TodoSample.List)!;   // why: the sample is a JSON document
        list["todos"] = new JsonArray(todos);
        _server.TodosAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
    }

    private static JsonNode Todo(string id, string text, string? due = null)
    {
        var todo = JsonNode.Parse(TodoSample.List)!["todos"]![3]!.DeepClone();   // "call the dentist": open, no date, no links
        todo["id"] = id;
        todo["text"] = text;
        todo["due"] = due;
        todo["addedBy"] = "human:ui";
        todo["lastBy"] = "human:ui";
        return todo;
    }

    // Seven open todos in the panel's order: one overdue, two today, one upcoming, three with no date. Five show.
    private static JsonNode[] Seven() =>
    [
        Todo("td_1", "renew the staging cert", "2026-10-08"),
        Todo("td_2", "check the flaky test", "2026-10-11"),
        Todo("td_3", "rotate the staging key", "2026-10-11"),
        Todo("td_4", "prep the demo script", "2026-10-13"),
        Todo("td_5", "call the dentist"),
        Todo("td_6", "book the room"),
        Todo("td_7", "water the plants"),
    ];

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected(int needsYou = 0)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = 1440, Height = 900 };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0 && !shell.Todo.IsLoading);
        if (needsYou > 0)
        {
            shell.ShowNeedsYou(new NeedsYouFeed([.. Enumerable.Range(0, needsYou).Select(i => NeedsYouRows.Park("park:ITM-" + i.ToString(CultureInfo.InvariantCulture)))], ""));
            Settle(window);
        }

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

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static Control Panel(Window window) => Named<Control>(window, "TodoPanelFrame");

    private static List<Border> RowFrames(Window window) =>
        [.. Panel(window).GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("todo-row") && b.IsEffectivelyVisible)];

    private static void FocusRow(Window window, int index)
    {
        _ = RowFrames(window)[index].Focus(NavigationMethod.Tab);
        Settle(window);
    }

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Settle(window);
    }

    // What has focus, as a word: a todo row by its text, another control by its name.
    private static string Focused(Window window) =>
        window.FocusManager?.GetFocusedElement() is Control c ? c.DataContext is TodoRowViewModel row ? row.Text : c.Name ?? c.GetType().Name : "(nothing)";

    private static bool Ringed(Control control) => control.Classes.Contains("region-focus");

    private static bool InNeedsYou(Window window) =>
        window.FocusManager?.GetFocusedElement() is Visual v && v.GetSelfAndVisualAncestors().OfType<NeedsYouRow>().Any();

    private static string[] Texts(Visual within, string name) =>
        [.. within.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == name && t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    [Fact]
    public Task Tab_in_the_panel_moves_focus_to_Needs_you_and_Shift_Tab_from_its_first_row_comes_back() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, shell) = await Connected(needsYou: 2);
        FocusRow(window, 1);
        Assert.True(shell.Todo.HasFocus);

        Key(window, PhysicalKey.Tab);

        Assert.True(InNeedsYou(window), Focused(window));
        Assert.True(shell.IsNeedsYouFocused);
        Assert.False(shell.Todo.HasFocus);

        Key(window, PhysicalKey.Tab, RawInputModifiers.Shift);

        Assert.Equal("check the flaky test", Focused(window));   // the panel's selected row, not its first
        Assert.True(shell.Todo.HasFocus);
        Assert.False(shell.IsNeedsYouFocused);
    });

    [Fact]
    public Task The_region_with_focus_draws_a_ring_and_its_heading_in_the_accent_colour() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, _) = await Connected(needsYou: 2);
        var needsYou = Named<Control>(window, "NeedsYouPanel");
        Assert.False(Ringed(Panel(window)));
        Assert.False(Ringed(needsYou));

        FocusRow(window, 0);

        Assert.True(Ringed(Panel(window)));
        Assert.True(Ringed(Named<TextBlock>(window, "TodoTitle")));
        Assert.False(Ringed(needsYou));

        Key(window, PhysicalKey.Tab);

        Assert.False(Ringed(Panel(window)));
        Assert.True(Ringed(needsYou));
    });

    [Fact]
    public Task J_and_K_and_the_arrows_walk_the_panels_stops_and_stop_at_the_last_one() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, _) = await Connected(needsYou: 2);
        FocusRow(window, 0);

        Key(window, PhysicalKey.J);
        Key(window, PhysicalKey.ArrowDown);
        Assert.Equal("rotate the staging key", Focused(window));
        Key(window, PhysicalKey.K);
        Key(window, PhysicalKey.ArrowUp);
        Assert.Equal("renew the staging cert", Focused(window));
        Key(window, PhysicalKey.K);
        Assert.Equal("renew the staging cert", Focused(window));   // the first stop holds

        for (var i = 0; i < 4; i++)
        {
            Key(window, PhysicalKey.J);
        }

        Assert.Equal("call the dentist", Focused(window));
        Key(window, PhysicalKey.J);
        Assert.Equal("TodoShowAll", Focused(window));
        Key(window, PhysicalKey.J);
        Assert.Equal("ElsewhereFrame", Focused(window));
        Key(window, PhysicalKey.J);
        Key(window, PhysicalKey.ArrowDown);
        Assert.Equal("ElsewhereFrame", Focused(window));   // never falls into Needs you
        Assert.False(InNeedsYou(window));
    });

    [Fact]
    public Task The_expanded_panel_has_Show_done_as_a_stop_after_its_rows() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, shell) = await Connected();
        shell.Todo.Expand();
        Settle(window);
        FocusRow(window, 6);

        Key(window, PhysicalKey.J);

        Assert.Equal("TodoDoneToggle", Focused(window));
    });

    [Fact]
    public Task X_checks_the_focused_row_off_sends_done_moves_focus_on_and_shows_the_Done_toast() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, shell) = await Connected();
        FocusRow(window, 0);

        Key(window, PhysicalKey.X);
        await Until(window, () => shell.Toast.Text == "Done");

        Assert.Equal(["/sindri/todos/td_1/done"], _server.TodoWrites.Select(w => w.Route), StringComparer.Ordinal);
        Assert.Equal("check the flaky test", Focused(window));
        Assert.Equal(["check the flaky test", "rotate the staging key", "prep the demo script", "call the dentist", "book the room"],
            RowFrames(window).Select(b => ((TodoRowViewModel)b.DataContext!).Text), StringComparer.Ordinal);   // the sixth todo slides into the five
        Assert.Equal(("Done", true), (shell.Toast.Text, shell.Toast.HasUndo));
    });

    [Fact]
    public Task Space_checks_off_and_Delete_and_Backspace_remove() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, shell) = await Connected();
        FocusRow(window, 0);

        Key(window, PhysicalKey.Space);
        Key(window, PhysicalKey.Delete);
        Key(window, PhysicalKey.Backspace);
        await Until(window, () => shell.Toast.Text == "Removed");

        Assert.Equal(["/sindri/todos/td_1/done", "/sindri/todos/td_2/remove", "/sindri/todos/td_3/remove"], _server.TodoWrites.Select(w => w.Route), StringComparer.Ordinal);
        Assert.Equal("Removed", shell.Toast.Text);
    });

    [Fact]
    public Task Delete_on_the_last_row_moves_focus_to_the_previous_one() => OnUi(async () =>
    {
        Serve(Seven()[..3]);
        var (window, _) = await Connected();
        FocusRow(window, 2);

        Key(window, PhysicalKey.Delete);

        Assert.Equal("check the flaky test", Focused(window));
    });

    [Fact]
    public Task A_failed_write_brings_the_row_back_with_the_reason_and_focus() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, _) = await Connected();
        _server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TODO-001", "Try again.");
        FocusRow(window, 0);

        Key(window, PhysicalKey.X);
        await Until(window, () => Texts(Panel(window), "TodoNotice").Length == 1);

        Assert.Equal(["Couldn't save: Try again."], Texts(Panel(window), "TodoNotice"));
        Assert.Equal("renew the staging cert", Focused(window));
    });

    [Fact]
    public Task Ctrl_Z_undoes_the_check_off_and_focus_is_back_on_the_todo() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, shell) = await Connected();
        FocusRow(window, 0);
        Key(window, PhysicalKey.X);

        Key(window, PhysicalKey.Z, RawInputModifiers.Control);
        await Until(window, () => _server.TodoWrites.Count == 2 && Focused(window) == "renew the staging cert");

        Assert.Equal("/sindri/todos/td_1/reopen", _server.TodoWrites[1].Route);
        Assert.Equal("td_1", shell.Todo.Rows[0].Id);
    });

    [Fact]
    public Task X_in_the_panel_never_rejects_a_Needs_you_decision() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision()], ""));
        _ = shell.Press("J");   // the decision is selected in Needs you
        Settle(window);
        FocusRow(window, 0);

        Key(window, PhysicalKey.X);
        await shell.Decision.Settled;

        Assert.Empty(_server.DecideCalls);
        _ = Assert.Single(_server.TodoWrites);
    });

    [Fact]
    public Task X_in_Needs_you_rejects_the_decision_and_checks_no_todo_off() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision()], ""));
        Settle(window);
        _ = Named<Control>(window, "NeedsYouPanel").GetVisualDescendants().OfType<NeedsYouRow>().First(r => r.IsEffectivelyVisible)
            .GetVisualDescendants().OfType<Border>().First(b => b.Name == "RowFrame").Focus(NavigationMethod.Tab);
        Settle(window);

        Key(window, PhysicalKey.X);
        await shell.Decision.Settled;

        Assert.Equal([("DEC-1", "decline")], [.. _server.DecideCalls.Select(c => (c.Decision, c.Request.Choice))]);
        Assert.Empty(_server.TodoWrites);
    });

    [Fact]
    public Task X_Delete_and_Space_do_nothing_on_Show_all_or_the_From_elsewhere_line() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, shell) = await Connected(needsYou: 1);
        _ = Named<Control>(window, "ElsewhereFrame").Focus(NavigationMethod.Tab);
        Settle(window);

        Key(window, PhysicalKey.X);
        Key(window, PhysicalKey.Delete);
        Key(window, PhysicalKey.Space);
        _ = Named<Control>(window, "TodoShowAll").Focus(NavigationMethod.Tab);
        Settle(window);
        Key(window, PhysicalKey.X);
        Key(window, PhysicalKey.Delete);

        Assert.Empty(_server.TodoWrites);
        Assert.Empty(_server.DecideCalls);
        Assert.False(shell.Todo.IsExpanded);
    });

    [Fact]
    public Task In_the_Done_group_X_reopens_a_done_todo_and_restores_a_removed_one() => OnUi(async () =>
    {
        var done = Todo("td_d", "renew the cert");
        done["doneAt"] = "2026-10-10T09:00:00Z";
        var removed = Todo("td_r", "book the offsite");
        removed["removed"] = true;
        removed["removedAt"] = "2026-10-11T08:00:00Z";
        Serve([.. Seven()[..2], done, removed]);
        var (window, shell) = await Connected();
        shell.Todo.Expand();
        shell.Todo.ToggleDone();
        Settle(window);
        FocusRow(window, 2);

        Key(window, PhysicalKey.X);
        Key(window, PhysicalKey.X);

        Assert.Equal(["/sindri/todos/td_d/reopen", "/sindri/todos/td_r/restore"], _server.TodoWrites.Select(w => w.Route), StringComparer.Ordinal);
    });

    [Fact]
    public Task Esc_cancels_a_waiting_G_chord_first_then_closes_the_quick_add_box_then_folds_the_panel() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, shell) = await Connected();
        shell.Todo.Expand();
        Settle(window);
        FocusRow(window, 0);

        Key(window, PhysicalKey.G);
        Assert.True(shell.IsChordPending);
        Key(window, PhysicalKey.Escape);
        Assert.False(shell.IsChordPending);
        Assert.True(shell.Todo.IsExpanded);

        Key(window, PhysicalKey.N);
        Assert.True(shell.QuickAdd.IsOpen);
        Key(window, PhysicalKey.Escape);
        Assert.False(shell.QuickAdd.IsOpen);
        Assert.True(shell.Todo.IsExpanded);

        Key(window, PhysicalKey.Escape);
        Assert.False(shell.Todo.IsExpanded);
        Assert.StartsWith("renew", Focused(window), StringComparison.Ordinal);
    });
}
