using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The note card when its reads fail (review of #2376): the first read, the library read, and a read that throws.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteCardReadFailureTests
{
    private readonly TodoNoteCardFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Before_the_first_read_ends_the_card_says_it_is_reading_the_setting()
    {
        var card = _f.Card();

        Assert.Equal((TodoNoteCardStage.Reading, true, "Reading the todo note setting…"), (card.Stage, card.ShowReading, card.Text.Reading));
    }

    [Fact]
    public async Task A_first_read_that_fails_shows_the_error_and_stops_saying_it_is_reading()
    {
        _f.Server.TodoNoteAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-API-500", "boom");
        var card = _f.Card();

        await card.Refresh(TodoNoteCardFixture.Url, Token);

        Assert.True(card.HasError);
        Assert.False(card.ShowReading);
    }

    [Fact]
    public async Task A_failed_read_after_a_good_one_leaves_the_card_as_it_was()
    {
        _f.ServeNote("syncing");
        var card = await _f.Read(Token);
        _f.Server.TodoNoteAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-API-500", "boom");

        await card.Refresh(TodoNoteCardFixture.Url, Token);

        Assert.Equal((TodoNoteCardStage.Syncing, false), (card.Stage, card.HasError));
    }

    [Fact]
    public async Task A_library_read_that_fails_shows_the_error_and_not_the_add_a_vault_sentence()
    {
        _f.Server.LibraryAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-API-500", "boom");
        var card = _f.Card();

        await card.Refresh(TodoNoteCardFixture.Url, Token);

        Assert.Equal((true, false, false), (card.HasError, card.ShowNoVaults, card.ShowReading));
    }

    [Fact]
    public async Task A_later_good_read_clears_the_read_error()
    {
        _f.Server.LibraryAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-API-500", "boom");
        var card = _f.Card();
        await card.Refresh(TodoNoteCardFixture.Url, Token);
        _f.ServeVaults("work", "inbox");

        await card.Refresh(TodoNoteCardFixture.Url, Token);

        Assert.Equal((false, TodoNoteCardStage.Form), (card.HasError, card.Stage));
    }

    [Fact]
    public async Task A_good_read_leaves_a_refusal_the_owner_is_reading_alone()
    {
        var card = await _f.Read(Token);
        _f.Server.TodoNoteCheckAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-TODO-002", "bad");
        await card.Check(Token);

        await card.Refresh(TodoNoteCardFixture.Url, Token);

        Assert.True(card.HasError);
    }

    [Fact]
    public async Task A_read_that_throws_is_shown_as_an_error_and_does_not_escape()
    {
        _f.Server.TodoNoteReadFailure = new InvalidOperationException("bug");
        var card = _f.Card();

        await card.Refresh(TodoNoteCardFixture.Url, Token);

        Assert.Equal((true, false), (card.HasError, card.ShowReading));
    }
}
