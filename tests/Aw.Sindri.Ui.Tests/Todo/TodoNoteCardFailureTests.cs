using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The note card when a call fails (review of #2376): a call that throws, a key that could not be taken back, and server text that must not reach the screen.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteCardFailureTests
{
    private readonly TodoNoteCardFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_check_that_throws_leaves_the_buttons_enabled_and_tells_the_view()
    {
        var card = await _f.Read(Token);
        _f.Server.TodoNoteCheckFailure = new InvalidOperationException("bug");
        var changed = new List<string?>();
        card.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => card.Check(Token));

        Assert.Equal((false, true), (card.IsBusy, card.CanCheck));
        Assert.Equal(2, changed.Count(n => string.Equals(n, nameof(TodoNoteCardViewModel.CanCheck), StringComparison.Ordinal)));   // once when the call starts, once when it ends
    }

    [Fact]
    public async Task A_stop_that_throws_leaves_the_buttons_enabled_and_tells_the_view()
    {
        _f.ServeNote("syncing");
        var card = await _f.Read(Token);
        card.AskStop();
        var changed = new List<string?>();
        card.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _f.Server.CommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopping = card.Stop(Token);
        _f.Server.CommandGate.SetException(new InvalidOperationException("bug"));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => stopping);

        Assert.False(card.IsBusy);
        Assert.Equal(2, changed.Count(n => string.Equals(n, nameof(TodoNoteCardViewModel.IsBusy), StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_key_that_could_not_be_taken_back_says_sync_is_on_and_shows_Stop_syncing()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 42), Token);
        _f.Server.TodoNoteImportAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-TODO-008", "nothing to import");
        _f.Server.TodoNoteStopAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-API-500", "boom");
        _f.Server.OnTodoNoteSetup = _ => _f.ServeNote("waiting for import");

        await card.Import(Token);

        Assert.Equal(["check", "set", "import", "stop"], _f.Calls);
        Assert.Contains("Stop syncing", card.ErrorText, StringComparison.Ordinal);
        Assert.Contains("nothing was imported", card.ErrorText, StringComparison.Ordinal);
        Assert.Equal((TodoNoteCardStage.Syncing, true), (card.Stage, card.ShowSyncing));
    }

    [Fact]
    public async Task A_key_that_was_taken_back_adds_no_extra_line()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 42), Token);
        _f.Server.TodoNoteImportAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-TODO-008", "nothing to import");

        await card.Import(Token);

        Assert.DoesNotContain("Stop syncing", card.ErrorText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SND-TODO-002")]
    [InlineData("SND-TODO-007")]
    [InlineData("SND-TODO-008")]
    [InlineData("SND-PROFILE-001")]
    [InlineData("SND-ZZZ-999")]
    public async Task A_refusal_never_shows_the_server_message(string code)
    {
        var card = await _f.Read(Token);
        _f.Server.TodoNoteCheckAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, code, "The note /home/me/Vaults/work/Todo.md is outside the vault");

        await card.Check(Token);

        Assert.True(card.HasError);
        Assert.DoesNotContain("/home/me", card.ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refusal_with_a_code_this_build_does_not_know_names_the_code()
    {
        var card = await _f.Read(Token);
        _f.Server.TodoNoteCheckAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-ZZZ-999", "secret detail");

        await card.Check(Token);

        Assert.Contains("SND-ZZZ-999", card.ErrorText, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", card.ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_check_shows_a_fixed_sentence_for_a_known_code()
    {
        var card = await _f.Read(Token);
        _f.Server.TodoNoteCheckAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-TODO-002", "The note is outside the vault. todo.obsidian.note");

        await card.Check(Token);

        Assert.Equal("That vault or note path isn't valid. Pick a note inside the vault.", card.ErrorText);
    }
}
