using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The note card when two things overlap (review of #2376): an answer that lands on fields the owner already edited, and a read that finishes after a click that changed the card.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteCardRaceTests
{
    private readonly TodoNoteCardFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_check_answer_is_dropped_when_the_note_was_edited_during_the_check()
    {
        var card = await _f.Read(Token);
        _f.Server.TodoNoteCheckGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checking = card.Check(Token);

        card.NoteText = "Work/Other.md";
        _f.Server.TodoNoteCheckGate.SetResult();
        await checking;

        Assert.Equal((TodoNoteCardStage.Form, false, false), (card.Stage, card.ShowImport, card.ShowResult));
        Assert.Equal(["check"], _f.Calls);
    }

    [Fact]
    public async Task A_check_answer_is_dropped_when_the_vault_was_changed_during_the_check()
    {
        var card = await _f.Read(Token);
        _f.Server.TodoNoteCheckGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checking = card.Check(Token);

        card.SelectedVault = "inbox";
        _f.Server.TodoNoteCheckGate.SetResult();
        await checking;

        Assert.Equal((TodoNoteCardStage.Form, false), (card.Stage, card.ShowResult));
    }

    [Fact]
    public async Task A_check_answer_is_kept_when_the_note_only_gained_spaces_around_it()
    {
        var card = await _f.Read(Token);
        _f.Server.TodoNoteCheckGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checking = card.Check(Token);

        card.NoteText = "Todo.md ";
        _f.Server.TodoNoteCheckGate.SetResult();
        await checking;

        Assert.Equal(TodoNoteCardStage.Checked, card.Stage);
    }

    [Fact]
    public async Task A_read_that_started_before_Stop_and_ended_after_it_leaves_the_card_on_the_fields()
    {
        _f.ServeNote("syncing");
        var card = await _f.Read(Token);
        Assert.Equal(TodoNoteCardStage.Syncing, card.Stage);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.GateNextLibraryRead(gate);
        var refreshing = card.Refresh(TodoNoteCardFixture.Url, Token);   // reads the note as syncing, then waits for the library

        card.AskStop();
        await card.Stop(Token);
        gate.SetResult();
        await refreshing;

        Assert.Equal(TodoNoteCardStage.Form, card.Stage);
    }

    [Fact]
    public async Task A_read_that_started_before_Start_and_ended_after_it_does_not_undo_it()
    {
        var card = await _f.Checked(TodoNoteSetupSample.NewNote, Token);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.GateNextLibraryRead(gate);
        var refreshing = card.Refresh(TodoNoteCardFixture.Url, Token);   // reads the note as not set, then waits for the library
        _f.Server.OnTodoNoteSetup = _ => _f.ServeNote("syncing");

        await card.Start(Token);
        gate.SetResult();
        await refreshing;

        Assert.Equal(TodoNoteCardStage.Syncing, card.Stage);
    }
}
