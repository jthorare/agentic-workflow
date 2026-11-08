using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>Settings' Obsidian todo note card, state by state (todo spec §3.4 "Settings card", §4.4): what each read shows, what Check note writes (nothing), and which buttons each check offers.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteCardTests
{
    private readonly TodoNoteCardFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_library_with_no_vault_says_to_add_one_first()
    {
        _f.ServeVaults();
        var card = _f.Card();

        await card.Refresh(TodoNoteCardFixture.Url, Token);

        Assert.Equal((TodoNoteCardStage.NoVaults, true, false), (card.Stage, card.ShowNoVaults, card.ShowForm));
        Assert.Equal("Add an Obsidian vault in Settings → Library first", card.Text.NoVaults);
    }

    [Fact]
    public void The_no_vault_button_asks_for_the_library_card()
    {
        _f.ServeVaults();
        var card = _f.Card();
        var asked = 0;
        card.LibraryRequested += (_, _) => asked++;

        card.OpenLibrary();

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task With_vaults_and_no_note_set_the_card_shows_the_fields_with_the_first_vault_and_Todo_md()
    {
        var card = await _f.Read(Token);

        Assert.Equal((TodoNoteCardStage.Form, true, true), (card.Stage, card.ShowForm, card.CanCheck));
        Assert.Equal(["work", "inbox"], card.Vaults);
        Assert.Equal(("work", "Todo.md"), (card.SelectedVault, card.NoteText));
        Assert.Empty(_f.Calls);
    }

    [Fact]
    public async Task A_note_that_is_set_shows_the_syncing_line_with_the_last_sync_time_and_Change_and_Stop()
    {
        _f.ServeNote("syncing");

        var card = await _f.Read(Token);

        Assert.Equal(TodoNoteCardStage.Syncing, card.Stage);
        Assert.Equal("Syncing with work/Todo.md · last synced 09:58", card.SyncingText);
        Assert.False(card.ShowForm);
    }

    [Fact]
    public async Task A_one_way_note_says_so_on_the_syncing_line()
    {
        _f.ServeNote("one way");

        var card = await _f.Read(Token);

        Assert.Equal("Syncing with work/Todo.md · one way · last synced 09:58", card.SyncingText);
    }

    [Fact]
    public async Task An_older_sindri_with_no_note_route_draws_no_card()
    {
        _f.Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.NotFound, string.Empty);
        var card = _f.Card();

        await card.Refresh(TodoNoteCardFixture.Url, Token);

        Assert.False(card.IsAvailable);
    }

    [Fact]
    public async Task Check_note_shows_Checking_the_note_in_the_live_region_and_writes_nothing()
    {
        var card = await _f.Read(Token);
        _f.Server.TodoNoteCheckGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var checking = card.Check(Token);

        Assert.Equal(("Checking the note…", true, false), (card.StatusText, card.IsBusy, card.CanCheck));
        _f.Server.TodoNoteCheckGate.SetResult();
        await checking;
        Assert.Equal((string.Empty, TodoNoteCardStage.Checked), (card.StatusText, card.Stage));
        Assert.Equal(["check"], _f.Calls);
    }

    [Fact]
    public async Task The_check_sends_the_vault_and_the_note_as_typed()
    {
        var card = await _f.Read(Token);
        card.SelectedVault = "inbox";
        card.NoteText = "Lists/Todo.md";

        await card.Check(Token);

        var sent = Assert.Single(_f.Server.TodoNoteSetup);
        Assert.Equal(new TodoNoteTarget("inbox", "Lists/Todo.md"), sent.Body);
    }

    [Fact]
    public async Task A_new_note_says_Sindri_will_create_it_and_offers_Start_syncing_only()
    {
        var card = await _f.Checked(TodoNoteSetupSample.NewNote, Token);

        Assert.Equal("Todo.md doesn't exist yet. Sindri will create it.", card.ResultText);
        Assert.Equal((true, false, false, true), (card.ShowStart, card.ShowImport, card.ShowStartOneWay, card.ShowCancel));
    }

    [Fact]
    public async Task A_note_with_no_open_tasks_also_offers_Start_syncing()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 0), Token);

        Assert.Equal("Todo.md has no open tasks.", card.ResultText);
        Assert.Equal((true, false), (card.ShowStart, card.ShowImport));
    }

    [Fact]
    public async Task A_note_with_open_tasks_shows_the_counts_and_Import_and_start_syncing_with_Cancel()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 42, sindriOpen: 6), Token);

        Assert.Equal("Import adds ids to 42 lines in Todo.md and adds your 6 Sindri todos after them", card.CountsText);
        Assert.Equal((false, true, true, false, false), (card.ShowStart, card.ShowImport, card.ShowCancel, card.HasCantImport, card.HasOverCap));
    }

    [Fact]
    public async Task The_tasks_that_cannot_be_imported_name_their_line_numbers()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 45, cantImport: [4, 17, 30]), Token);

        Assert.Equal("3 lines can't be imported (lines 4, 17, 30): they have a markdown link or a 📅 inside the text", card.CantImportText);
    }

    [Fact]
    public async Task Past_five_lines_the_list_stops_and_points_at_sindri_doctor()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 45, cantImport: [4, 17, 30, 31, 40, 52, 60]), Token);

        Assert.Equal("7 lines can't be imported (lines 4, 17, 30, 31, 40 and 2 more: see `sindri doctor`): they have a markdown link or a 📅 inside the text", card.CantImportText);
    }

    [Fact]
    public async Task Past_the_cap_the_card_says_how_many_will_import()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 600, willImport: 458, cap: 500), Token);

        Assert.Equal("Only 458 will import: 500 open todos is the limit", card.OverCapText);
        Assert.True(card.ShowImport);
    }

    [Fact]
    public async Task A_disk_that_cannot_swap_files_says_so_turns_Import_off_and_offers_Start_syncing_one_way()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 12, canSwap: false), Token);

        Assert.Equal("Sindri can't write to Todo.md on this disk. It can bring in ticks and edits of lines that already have an id, and can't add new ones", card.OneWayText);
        Assert.Equal((false, false, true, true), (card.ShowStart, card.ShowImport, card.ShowStartOneWay, card.ShowCancel));
        Assert.Equal("Start syncing (one way)", card.Text.StartOneWay);
    }

    [Fact]
    public async Task Cancel_drops_the_check_and_leaves_no_key()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(), Token);

        card.Cancel();

        Assert.Equal((TodoNoteCardStage.Form, false), (card.Stage, card.ShowResult));
        Assert.Equal(["check"], _f.Calls);
    }

    [Fact]
    public async Task Typing_in_a_field_after_a_check_drops_the_check()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(), Token);

        card.NoteText = "Other.md";

        Assert.Equal((TodoNoteCardStage.Form, false), (card.Stage, card.ShowImport));
    }

    [Fact]
    public async Task A_refused_check_shows_the_reason_inline_and_stays_on_the_fields()
    {
        var card = await _f.Read(Token);
        _f.Server.TodoNoteCheckAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-TODO-002", "The note is outside the vault. todo.obsidian.note");

        await card.Check(Token);

        Assert.Equal(("The note is outside the vault. todo.obsidian.note", true, TodoNoteCardStage.Form), (card.ErrorText, card.HasError, card.Stage));
    }

    [Fact]
    public async Task Start_syncing_saves_the_key_and_nothing_else()
    {
        var card = await _f.Checked(TodoNoteSetupSample.NewNote, Token);
        _f.Server.OnTodoNoteSetup = _ => _f.ServeNote("syncing");

        await card.Start(Token);

        Assert.Equal(["check", "set"], _f.Calls);
        Assert.Equal(new TodoNoteSetBody("work", "Todo.md", IfHash: null), _f.Server.TodoNoteSetup[1].Body);
        Assert.Equal(TodoNoteCardStage.Syncing, card.Stage);
    }

    [Fact]
    public async Task Start_syncing_one_way_saves_the_key_and_ends_on_the_one_way_line()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 3, canSwap: false), Token);
        _f.Server.OnTodoNoteSetup = _ => _f.ServeNote("one way");

        await card.Start(Token);

        Assert.Equal(["check", "set"], _f.Calls);
        Assert.Contains("one way", card.SyncingText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_save_shows_the_reason_and_keeps_the_check()
    {
        var card = await _f.Checked(TodoNoteSetupSample.NewNote, Token);
        _f.Server.TodoNoteSetAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-PROFILE-001", "Couldn't read the profile (fix: sindri doctor)");

        await card.Start(Token);

        Assert.Equal(("Couldn't read the profile (fix: sindri doctor)", TodoNoteCardStage.Checked), (card.ErrorText, card.Stage));
    }

    [Fact]
    public async Task Import_and_start_syncing_saves_the_key_then_imports_with_the_open_count_shown()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 42), Token);
        _f.Server.OnTodoNoteSetup = _ => _f.ServeNote("syncing");

        await card.Import(Token);

        Assert.Equal(["check", "set", "import"], _f.Calls);
        Assert.Equal(new TodoNoteImportBody("work", "Todo.md", 42), _f.Server.TodoNoteSetup[2].Body);
        Assert.Equal(TodoNoteCardStage.Syncing, card.Stage);
    }

    [Fact]
    public async Task A_note_that_changed_since_the_check_says_the_new_count_takes_the_key_back_and_asks_again()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 42), Token);
        _f.Server.TodoNoteCheckAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.Check(open: 44));
        _f.Server.TodoNoteImportAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The note has 44 open tasks, not 42.");

        await card.Import(Token);

        Assert.Equal(["check", "set", "import", "stop", "check"], _f.Calls);
        Assert.Equal(("Todo.md changed: now 44 open tasks", TodoNoteCardStage.Checked, true), (card.StatusText, card.Stage, card.ShowImport));
        Assert.StartsWith("Import adds ids to 44 lines", card.CountsText, StringComparison.Ordinal);
        Assert.False(card.HasError);
    }

    [Fact]
    public async Task Change_opens_the_fields_on_the_running_note_and_Cancel_goes_back_with_no_call()
    {
        _f.ServeNote("syncing");
        var card = await _f.Read(Token);

        card.Change();

        Assert.Equal((TodoNoteCardStage.Form, "work", "Todo.md", true), (card.Stage, card.SelectedVault, card.NoteText, card.ShowCancel));
        card.Cancel();
        Assert.Equal(TodoNoteCardStage.Syncing, card.Stage);
        Assert.Empty(_f.Calls);
    }

    [Fact]
    public async Task Stop_syncing_asks_first_and_Keep_syncing_sends_nothing()
    {
        _f.ServeNote("syncing");
        var card = await _f.Read(Token);

        card.AskStop();

        Assert.Equal(("Stop syncing with Todo.md? The note stays as it is.", true), (card.StopAskText, card.ShowStopAsk));
        card.KeepSyncing();
        Assert.Equal((false, TodoNoteCardStage.Syncing), (card.ShowStopAsk, card.Stage));
        Assert.Empty(_f.Calls);
    }

    [Fact]
    public async Task Confirmed_Stop_syncing_removes_the_key_and_brings_the_fields_back()
    {
        _f.ServeNote("syncing");
        var card = await _f.Read(Token);
        card.AskStop();

        await card.Stop(Token);

        Assert.Equal(["stop"], _f.Calls);
        Assert.Equal((TodoNoteCardStage.Form, false), (card.Stage, card.ShowStopAsk));
    }

    [Fact]
    public async Task A_read_while_a_check_is_shown_does_not_disturb_it()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(), Token);

        await card.Refresh(TodoNoteCardFixture.Url, Token);

        Assert.Equal((TodoNoteCardStage.Checked, true), (card.Stage, card.ShowImport));
    }
}
