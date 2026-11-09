using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The note card and the profile hash (todo spec §4.4): the card sends the last profileHash it got as ifHash on the next set or stop; a 409 means the profile moved, so the card reads again and says why.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteCardProfileHashTests
{
    private const string Why = "Settings changed somewhere else, so Sindri read them again. Try again if it still looks right.";

    private readonly TodoNoteCardFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // A card that checked a note and started syncing; the set answered the hash "h1".
    private async Task<TodoNoteCardViewModel> Syncing()
    {
        var card = await _f.Checked(TodoNoteSetupSample.NewNote, Token);
        _f.Server.TodoNoteSetAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.ChangedTo("h1"));
        _f.Server.OnTodoNoteSetup = _ => _f.ServeNote("syncing");
        await card.Start(Token);
        return card;
    }

    [Fact]
    public async Task The_first_set_sends_no_ifHash()
    {
        _ = await Syncing();

        Assert.Equal(new TodoNoteSetBody("work", "Todo.md", IfHash: null), _f.Server.TodoNoteSetup[1].Body);
    }

    [Fact]
    public async Task The_next_set_sends_the_hash_the_last_set_answered()
    {
        var card = await Syncing();
        card.Change();
        card.NoteText = "Other.md";
        await card.Check(Token);

        await card.Start(Token);

        Assert.Equal(new TodoNoteSetBody("work", "Other.md", "h1"), _f.Server.TodoNoteSetup[^1].Body);
    }

    [Fact]
    public async Task Stop_sends_the_hash_the_last_set_answered()
    {
        var card = await Syncing();
        _f.Server.TodoNoteStopAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.ChangedTo("h2"));
        card.AskStop();

        await card.Stop(Token);

        Assert.Equal(new TodoNoteStopBody("h1"), _f.Server.TodoNoteSetup[^1].Body);
    }

    [Fact]
    public async Task A_stop_after_a_stop_sends_the_stop_answers_hash()
    {
        var card = await Syncing();
        _f.Server.TodoNoteStopAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.ChangedTo("h2"));
        card.AskStop();
        await card.Stop(Token);
        _f.Server.TodoNoteSetAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.ChangedTo("h3"));
        await card.Check(Token);

        await card.Start(Token);

        Assert.Equal(new TodoNoteSetBody("work", "Todo.md", "h2"), _f.Server.TodoNoteSetup[^1].Body);
    }

    [Fact]
    public async Task A_409_on_the_set_reads_the_note_and_checks_again_and_says_why()
    {
        var card = await _f.Checked(TodoNoteSetupSample.NewNote, Token);
        var reads = _f.Server.TodoNoteCalls;
        _f.Server.TodoNoteSetAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The profile changed (fix: read it again)");

        await card.Start(Token);

        Assert.Equal(["check", "set", "check"], _f.Calls);
        Assert.Equal(reads + 1, _f.Server.TodoNoteCalls);
        Assert.Equal((Why, TodoNoteCardStage.Checked, false), (card.StatusText, card.Stage, card.HasError));
    }

    [Fact]
    public async Task A_409_on_the_stop_reads_the_note_again_and_says_why()
    {
        var card = await Syncing();
        var reads = _f.Server.TodoNoteCalls;
        _f.Server.TodoNoteStopAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The profile changed (fix: read it again)");
        card.AskStop();

        await card.Stop(Token);

        Assert.Equal(reads + 1, _f.Server.TodoNoteCalls);
        Assert.Equal((Why, TodoNoteCardStage.Syncing, false), (card.StatusText, card.Stage, card.HasError));
    }

    [Fact]
    public async Task After_a_409_the_retry_sends_no_ifHash_because_the_card_read_the_profile_afresh()
    {
        var card = await Syncing();
        card.Change();
        card.NoteText = "Other.md";
        await card.Check(Token);
        _f.Server.TodoNoteSetAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The profile changed (fix: read it again)");
        await card.Start(Token);
        _f.Server.TodoNoteSetAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.ChangedTo("h9"));

        await card.Start(Token);

        Assert.Equal(new TodoNoteSetBody("work", "Other.md", IfHash: null), _f.Server.TodoNoteSetup[^1].Body);
    }
}
