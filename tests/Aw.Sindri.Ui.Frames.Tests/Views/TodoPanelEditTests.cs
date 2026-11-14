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
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Todo;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Edit in place and the link list on Home under the headless platform (todo spec §3.3, §3.4): Enter opens the edit, Enter saves, Esc cancels, a typed X is text, O opens the links.</summary>
[Lane(TestLane.Integration)]
public sealed class TodoPanelEditTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);   // Sunday 2026-10-11

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TodoPanelEditTests).Assembly)
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




    private static TextBox Field(Window window, string name) =>
        Panel(window).GetVisualDescendants().OfType<TextBox>().First(t => t.Name == name && t.IsEffectivelyVisible);

    private static bool Editing(Window window) =>
        Panel(window).GetVisualDescendants().OfType<TextBox>().Any(t => t.Name == "TodoEditText" && t.IsEffectivelyVisible);

    private static bool ListShown(Window window) =>
        Panel(window).GetVisualDescendants().OfType<Control>().Any(c => c.Name == "TodoLinkList" && c.IsEffectivelyVisible);

    // A todo with two links (the golden's "rotate the staging key"), due today.
    private static JsonNode Linked()
    {
        var todo = JsonNode.Parse(TodoSample.List)!["todos"]![2]!.DeepClone();
        todo["due"] = "2026-10-11";
        return todo;
    }

    [Fact]
    public Task Enter_opens_the_edit_with_the_text_focused_and_Enter_again_saves_what_was_typed() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, _) = await Connected();
        FocusRow(window, 0);

        Key(window, PhysicalKey.Enter);

        Assert.True(Editing(window));
        var box = Field(window, "TodoEditText");
        Assert.Same(box, window.FocusManager?.GetFocusedElement());
        box.Text = "renew the production cert";
        Settle(window);
        Key(window, PhysicalKey.Enter);
        await Until(window, () => _server.TodoWrites.Count == 1);

        var sent = Assert.IsType<TodoChange>(Assert.Single(_server.TodoWrites).Body);
        Assert.Equal("renew the production cert", sent.Text);
        Assert.Null(sent.Due);
        await Until(window, () => !Editing(window));
        Assert.Equal("renew the production cert", Focused(window));
    });

    [Fact]
    public Task Esc_cancels_the_edit_sends_nothing_and_keeps_focus_on_the_row() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, shell) = await Connected();
        FocusRow(window, 0);
        Key(window, PhysicalKey.Enter);
        Field(window, "TodoEditText").Text = "never saved";
        Settle(window);

        Key(window, PhysicalKey.Escape);

        await Until(window, () => !Editing(window) && Focused(window) == "renew the staging cert");
        Assert.Empty(_server.TodoWrites);
        Assert.False(shell.Todo.IsExpanded);
    });

    [Fact]
    public Task Tab_goes_from_the_text_to_the_day_and_on_to_the_links() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, _) = await Connected();
        FocusRow(window, 0);
        Key(window, PhysicalKey.Enter);

        Key(window, PhysicalKey.Tab);
        Assert.Same(Field(window, "TodoEditDue"), window.FocusManager?.GetFocusedElement());
        Key(window, PhysicalKey.Tab);
        Assert.Same(Field(window, "TodoEditLinks"), window.FocusManager?.GetFocusedElement());
    });

    [Fact]
    public Task X_and_J_and_O_typed_in_the_field_are_text_and_check_nothing_off() => OnUi(async () =>
    {
        Serve(Seven());
        var (window, shell) = await Connected();
        FocusRow(window, 0);
        Key(window, PhysicalKey.Enter);
        var box = Field(window, "TodoEditText");
        box.SelectionStart = box.SelectionEnd = box.CaretIndex = box.Text!.Length;   // why: the text exists, it was filled from the todo

        foreach (var (key, text) in new[] { (PhysicalKey.X, "x"), (PhysicalKey.J, "j"), (PhysicalKey.O, "o") })
        {
            window.KeyPressQwerty(key, RawInputModifiers.None);
            window.KeyTextInput(text);
            Settle(window);
        }

        Assert.EndsWith("xjo", box.Text, StringComparison.Ordinal);
        Assert.Empty(_server.TodoWrites);
        Assert.False(shell.Todo.LinkList.IsOpen);
        Assert.Same(box, window.FocusManager?.GetFocusedElement());
    });

    [Fact]
    public Task O_opens_the_list_of_links_and_Esc_closes_it_before_the_panel_folds() => OnUi(async () =>
    {
        Serve(Linked(), Todo("td_2", "water the plants"));
        var (window, shell) = await Connected();
        shell.Todo.Expand();
        Settle(window);
        FocusRow(window, 0);

        Key(window, PhysicalKey.O);

        Assert.True(ListShown(window));
        Key(window, PhysicalKey.J);
        Assert.Equal(1, shell.Todo.LinkList.SelectedIndex);
        Assert.Equal("rotate the staging key", Focused(window));   // J moved the list's selection, not the row focus
        Key(window, PhysicalKey.Escape);
        Assert.False(ListShown(window));
        Assert.True(shell.Todo.IsExpanded);
        Key(window, PhysicalKey.Escape);
        Assert.False(shell.Todo.IsExpanded);
    });

    // #2761: selecting a link changes only its colours. Each chip's text sits at the same offset inside the chip, and the
    // chip keeps its size, whether the chip is selected or not.
    [Fact]
    public Task Selecting_a_link_never_moves_its_text_or_resizes_its_chip() => OnUi(async () =>
    {
        Serve(Linked(), Todo("td_2", "water the plants"));
        var (window, shell) = await Connected();
        shell.Todo.Expand();
        Settle(window);
        FocusRow(window, 0);
        Key(window, PhysicalKey.O);
        Assert.True(ListShown(window));
        var first = Chips(window)[0];
        var second = Chips(window)[1];
        var (selectedText, selectedSize) = (TextOffset(first), first.Bounds.Size);

        Assert.Equal(selectedText, TextOffset(second));
        Key(window, PhysicalKey.J);
        Assert.Equal(1, shell.Todo.LinkList.SelectedIndex);
        Assert.Equal(selectedText, TextOffset(first));
        Assert.Equal(selectedSize, first.Bounds.Size);
        Assert.Equal(selectedText, TextOffset(second));
    });

    private static List<Border> Chips(Window window) =>
        [.. Named<Control>(window, "TodoLinkItems").GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("phrase-chip") && b.IsEffectivelyVisible)];

    // Where the chip's text starts inside the chip.
    private static Point TextOffset(Border chip) =>
        chip.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "TodoLinkRef").TranslatePoint(default, chip)!.Value;
}
