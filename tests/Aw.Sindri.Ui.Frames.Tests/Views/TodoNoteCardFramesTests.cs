using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Linear;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Todo;
using Aw.Sindri.Ui.Views;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Settings' Obsidian todo note card under the headless platform (todo spec §3.4 "Settings card", §3.5): one frame test per state, with the words, the buttons that are drawn and the live region.</summary>
[Lane(TestLane.Integration)]
public sealed class TodoNoteCardFramesTests : IDisposable
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly FakeSindriApi _server = new() { TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.NoteIn("not set")) };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TodoNoteCardFramesTests).Assembly)
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

    // The library golden has the vaults "work" and "inbox"; `vaults: false` takes them out.
    private void Library(bool vaults)
    {
        if (vaults)
        {
            return;
        }

        var library = JsonNode.Parse(RouteAnswer.Api("library", "ok").Body)!;   // why: the golden is a JSON document
        var roots = library["roots"]!.AsArray();   // why: as above
        foreach (var root in roots.Where(r => string.Equals((string?)r!["kind"], "vault", StringComparison.Ordinal)).ToList())
        {
            _ = roots.Remove(root);
        }

        _server.LibraryAnswer = new RouteAnswer(HttpStatusCode.OK, library.ToJsonString());
    }

    private async Task<(Window Window, TodoNoteCardViewModel Card)> Show(bool vaults = true, bool importServed = true)
    {
        Library(vaults);
        var card = new TodoNoteCardViewModel(() => _api, _server, _clock, importServed);
        await card.Refresh(_api, Token);
        var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _ => { }) { TodoNoteCard = card };
        var window = new Window { Width = 900, Height = 1200, Content = new SettingsView { DataContext = settings } };
        _windows.Add(window);
        window.Show();
        Settle(window);
        return (window, card);
    }

    private async Task<(Window Window, TodoNoteCardViewModel Card)> Checked(string check, bool importServed = true)
    {
        var (window, card) = await Show(importServed: importServed);
        _server.TodoNoteCheckAnswer = new RouteAnswer(HttpStatusCode.OK, check);
        Press(window, "TodoNoteCheck");
        await Until(window, () => card.Stage == TodoNoteCardStage.Checked);
        return (window, card);
    }

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

    private static T Find<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    private static bool Shown(Window window, string name) => window.GetVisualDescendants().OfType<Control>().Any(c => string.Equals(c.Name, name, StringComparison.Ordinal) && c.IsEffectivelyVisible);

    private static string Said(Window window, string name) => Find<TextBlock>(window, name).Text ?? string.Empty;

    private static void Press(Window window, string name)
    {
        Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
    }

    [Fact]
    public Task With_no_vault_the_card_says_to_add_one_in_Library_and_offers_the_button() => OnUi(async () =>
    {
        var (window, _) = await Show(vaults: false);

        Assert.Equal("Add an Obsidian vault in Settings → Library first", Said(window, "TodoNoteNoVaults"));
        Assert.True(Shown(window, "TodoNoteOpenLibrary"));
        Assert.False(Shown(window, "TodoNoteCheck"));
    });

    [Fact]
    public Task Before_saving_the_card_draws_the_vault_the_note_and_Check_note_and_nothing_else() => OnUi(async () =>
    {
        var (window, _) = await Show();

        Assert.True(Shown(window, "TodoNoteVault"));
        Assert.Equal("Todo.md", Find<TextBox>(window, "TodoNoteField").Text);
        Assert.Equal(("Check note", "Obsidian vault"), (Find<Button>(window, "TodoNoteCheck").Content, AutomationProperties.GetName(Find<ComboBox>(window, "TodoNoteVault"))));
        Assert.All(["TodoNoteStart", "TodoNoteImport", "TodoNoteCancel", "TodoNoteChange", "TodoNoteStop"], name => Assert.False(Shown(window, name), name));
    });

    [Fact]
    public Task The_status_line_is_a_polite_live_region_that_says_Checking_the_note_while_the_call_runs() => OnUi(async () =>
    {
        var (window, card) = await Show();
        _server.TodoNoteCheckGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Press(window, "TodoNoteCheck");
        await Until(window, () => card.IsBusy);

        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(Find<TextBlock>(window, "TodoNoteStatus")));
        Assert.Equal("Checking the note…", Said(window, "TodoNoteStatus"));
        _server.TodoNoteCheckGate.SetResult();
        await Until(window, () => !card.IsBusy);
    });

    private static readonly string[] _settingsStatusRows = ["TodoNoteStatus", "TodoNoteError", "LinearStatus", "LinearError"];
    private static readonly string[] _linearStatusRows = ["LinearStatus", "LinearError"];

    [Fact]
    public Task An_empty_status_or_error_row_takes_no_height_on_Settings_3454() => OnUi(async () =>
    {
        var (window, card) = await Show();
        Assert.True(card.StatusText.Length == 0 && card.ErrorText.Length == 0);

        var heights = _settingsStatusRows.SelectMany(n => window.GetVisualDescendants().OfType<Control>().Where(c => string.Equals(c.Name, n, StringComparison.Ordinal)))
            .Where(c => c.Bounds.Height > 0)
            .Select(c => c.Name)
            .ToList();

        Assert.Empty(heights);
    });

    [Fact]
    public Task An_empty_Linear_status_or_error_row_takes_no_height_on_Settings_3454() => OnUi(async () =>
    {
        _server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "not-connected");
        var linear = new LinearConnectionViewModel(() => _api, _server);
        await linear.Refresh(_api, Token);
        var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _ => { }) { LinearConnection = linear };
        var window = new Window { Width = 900, Height = 1200, Content = new SettingsView { DataContext = settings } };
        _windows.Add(window);
        window.Show();
        Settle(window);

        var rows = _linearStatusRows
            .Select(n => window.GetVisualDescendants().OfType<Control>().SingleOrDefault(c => string.Equals(c.Name, n, StringComparison.Ordinal)))
            .ToList();

        Assert.All(rows, r => Assert.NotNull(r));
        Assert.DoesNotContain(rows, r => r!.Bounds.Height > 0);
    });

    [Fact]
    public Task A_new_note_says_Sindri_will_create_it_with_Start_syncing() => OnUi(async () =>
    {
        var (window, _) = await Checked(TodoNoteSetupSample.NewNote);

        Assert.Equal("Todo.md doesn't exist yet. Sindri will create it.", Said(window, "TodoNoteResult"));
        Assert.True(Shown(window, "TodoNoteStart"));
        Assert.False(Shown(window, "TodoNoteImport"));
    });

    [Fact]
    public Task A_note_with_no_open_tasks_offers_Start_syncing() => OnUi(async () =>
    {
        var (window, _) = await Checked(TodoNoteSetupSample.Check(open: 0));

        Assert.Equal("Todo.md has no open tasks.", Said(window, "TodoNoteResult"));
        Assert.True(Shown(window, "TodoNoteStart"));
    });

    [Fact]
    public Task A_note_with_open_tasks_shows_the_counts_the_lines_that_cannot_import_the_cap_and_Import_with_Cancel() => OnUi(async () =>
    {
        var (window, _) = await Checked(TodoNoteSetupSample.Check(open: 600, cantImport: [4, 17, 30], willImport: 458, cap: 500, sindriOpen: 6));

        Assert.Equal("Import adds ids to 458 lines in Todo.md and adds your 6 Sindri todos after them", Said(window, "TodoNoteCounts"));
        Assert.StartsWith("3 lines can't be imported (lines 4, 17, 30)", Said(window, "TodoNoteCantImport"), StringComparison.Ordinal);
        Assert.Equal("Only 458 will import: 500 open todos is the limit", Said(window, "TodoNoteOverCap"));
        Assert.True(Shown(window, "TodoNoteImport"));
        Assert.True(Shown(window, "TodoNoteCancel"));
        Assert.False(Shown(window, "TodoNoteStart"));
    });

    [Fact]
    public Task Cancel_goes_back_to_the_fields_and_sends_nothing_more() => OnUi(async () =>
    {
        var (window, card) = await Checked(TodoNoteSetupSample.Check());

        Press(window, "TodoNoteCancel");

        Assert.Equal(TodoNoteCardStage.Form, card.Stage);
        Assert.False(Shown(window, "TodoNoteImport"));
        _ = Assert.Single(_server.TodoNoteSetup);
    });

    [Fact]
    public Task A_note_that_changed_before_Import_says_the_new_count_in_the_live_region_and_asks_again() => OnUi(async () =>
    {
        var (window, card) = await Checked(TodoNoteSetupSample.Check(open: 42));
        _server.TodoNoteCheckAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.Check(open: 44));
        _server.TodoNoteImportAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The note has 44 open tasks, not 42.");

        Press(window, "TodoNoteImport");
        await Until(window, () => !card.IsBusy && card.StatusText.Length > 0);

        Assert.Equal("Todo.md changed: now 44 open tasks", Said(window, "TodoNoteStatus"));
        Assert.True(Shown(window, "TodoNoteImport"));
    });

    [Fact]
    public Task A_disk_with_no_atomic_swap_says_so_turns_Import_off_and_offers_Start_syncing_one_way() => OnUi(async () =>
    {
        var (window, _) = await Checked(TodoNoteSetupSample.Check(open: 12, canSwap: false));

        Assert.StartsWith("Sindri can't write to Todo.md on this disk.", Said(window, "TodoNoteOneWay"), StringComparison.Ordinal);
        Assert.Equal("Start syncing (one way)", Find<Button>(window, "TodoNoteStartOneWay").Content);
        Assert.False(Shown(window, "TodoNoteImport"));
    });

    [Fact]
    public Task A_refused_check_shows_the_plain_reason_inline_in_the_error_colour() => OnUi(async () =>
    {
        var (window, card) = await Show();
        _server.TodoNoteCheckAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-TODO-002", "The note is outside the vault. todo.obsidian.note");

        Press(window, "TodoNoteCheck");
        await Until(window, () => card.HasError);

        Assert.Equal("That vault or note path isn't valid. Pick a note inside the vault.", Said(window, "TodoNoteError"));
    });

    [Fact]
    public Task A_running_sync_shows_the_vault_and_note_the_last_sync_and_Change_and_Stop_syncing() => OnUi(async () =>
    {
        _server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.NoteIn("syncing"));
        var (window, _) = await Show();

        Assert.Equal("Syncing with work/Todo.md · last synced 09:58", Said(window, "TodoNoteSyncing"));
        Assert.True(Shown(window, "TodoNoteChange"));
        Assert.True(Shown(window, "TodoNoteStop"));
        Assert.False(Shown(window, "TodoNoteCheck"));
    });

    [Fact]
    public Task Stop_syncing_asks_first_and_says_the_note_stays_as_it_is() => OnUi(async () =>
    {
        _server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.NoteIn("syncing"));
        var (window, _) = await Show();

        Press(window, "TodoNoteStop");

        Assert.Equal("Stop syncing with Todo.md? The note stays as it is.", Said(window, "TodoNoteStopAsk"));
        Assert.True(Shown(window, "TodoNoteKeep"));
        Assert.Empty(_server.TodoNoteSetup);
    });

    [Fact]
    public Task The_vault_and_note_fields_are_off_while_a_call_runs() => OnUi(async () =>
    {
        var (window, card) = await Show();
        _server.TodoNoteCheckGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Press(window, "TodoNoteCheck");
        await Until(window, () => card.IsBusy);

        Assert.False(Find<ComboBox>(window, "TodoNoteVault").IsEnabled);
        Assert.False(Find<TextBox>(window, "TodoNoteField").IsEnabled);
        _server.TodoNoteCheckGate.SetResult();
        await Until(window, () => !card.IsBusy);
        Assert.True(Find<TextBox>(window, "TodoNoteField").IsEnabled);
    });

    [Fact]
    public Task A_card_that_has_not_read_yet_shows_the_reading_line() => OnUi(async () =>
    {
        var card = new TodoNoteCardViewModel(() => _api, _server, _clock, importServed: true);
        var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _ => { }) { TodoNoteCard = card };
        var window = new Window { Width = 900, Height = 1200, Content = new SettingsView { DataContext = settings } };
        _windows.Add(window);
        window.Show();
        Settle(window);

        Assert.Equal("Reading the todo note setting…", Said(window, "TodoNoteReading"));
        Assert.True(Shown(window, "TodoNoteReading"));
        Assert.False(Shown(window, "TodoNoteCheck"));
        await Task.CompletedTask;
    });

    [Fact]
    public Task A_card_whose_first_read_failed_shows_the_error_and_not_the_reading_line() => OnUi(async () =>
    {
        _server.TodoNoteAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-API-500", "boom");
        var (window, card) = await Show();

        Assert.False(Shown(window, "TodoNoteReading"));
        Assert.Equal("Sindri refused that (SND-API-500). Run `sindri doctor` for details.", Said(window, "TodoNoteError"));
        Assert.True(card.HasError);
    });

    [Fact]
    public Task With_import_not_served_a_note_with_open_tasks_shows_the_line_and_Cancel_and_focus_lands_on_Cancel() => OnUi(async () =>
    {
        var (window, _) = await Checked(TodoNoteSetupSample.Check(open: 42), importServed: false);

        Assert.False(Shown(window, "TodoNoteImport"));
        Assert.False(Shown(window, "TodoNoteStartOneWay"));
        Assert.Equal("Import comes with note sync. Notes with open tasks can't sync yet.", Said(window, "TodoNoteImportLater"));
        Assert.True(Shown(window, "TodoNoteImportLater"));
        Assert.True(Find<Button>(window, "TodoNoteCancel").IsFocused);
    });

    [Fact]
    public Task After_Check_note_focus_moves_to_the_first_button_of_the_result() => OnUi(async () =>
    {
        var (window, card) = await Show();

        Press(window, "TodoNoteCheck");
        await Until(window, () => card.Stage == TodoNoteCardStage.Checked);

        Assert.True(Find<Button>(window, "TodoNoteStart").IsFocused);
    });

    [Fact]
    public Task After_Cancel_focus_returns_to_the_note_field() => OnUi(async () =>
    {
        var (window, _) = await Checked(TodoNoteSetupSample.Check());

        Press(window, "TodoNoteCancel");

        Assert.True(Find<TextBox>(window, "TodoNoteField").IsFocused);
    });

    [Fact]
    public Task After_Stop_syncing_is_asked_focus_moves_to_Keep_syncing() => OnUi(async () =>
    {
        _server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.NoteIn("syncing"));
        var (window, _) = await Show();

        Press(window, "TodoNoteStop");

        Assert.True(Find<Button>(window, "TodoNoteKeep").IsFocused);
    });

    [Fact]
    public Task After_Start_syncing_focus_moves_to_Change() => OnUi(async () =>
    {
        var (window, card) = await Checked(TodoNoteSetupSample.NewNote);
        _server.OnTodoNoteSetup = _ => _server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.NoteIn("syncing"));

        Press(window, "TodoNoteStart");
        await Until(window, () => card.Stage == TodoNoteCardStage.Syncing);

        Assert.True(Find<Button>(window, "TodoNoteChange").IsFocused);
    });

    [Fact]
    public Task The_first_load_does_not_take_focus() => OnUi(async () =>
    {
        var (window, _) = await Show();

        Assert.False(Find<TextBox>(window, "TodoNoteField").IsFocused);
    });

    [Fact]
    public Task Closing_the_window_takes_the_library_handler_off_the_long_lived_card() => OnUi(async () =>
    {
        var (window, card) = await Show();
        var field = typeof(TodoNoteCardViewModel).GetField(nameof(TodoNoteCardViewModel.LibraryRequested), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;   // why: the field-like event's delegate, to count its handlers
        Assert.Equal(1, ((Delegate?)field.GetValue(card))?.GetInvocationList().Length);

        window.Close();
        Settle(window);

        Assert.Null((Delegate?)field.GetValue(card));
    });
}
