using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Health's Context index card shows the server's reason for a source that needs a look, as served (#3911); a source without one reads as before. The word's mark (#4129): attention for "needs a look", the check for "ok", none when unreadable.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextIndexLookWordsTests
{
    private const string Words = "linear hasn't finished a sync in 10 minutes";   // why: the server's sentence, read back whole; the card adds no words of its own

    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ContextIndexCardViewModel> Read(string lookWords)
    {
        _server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK,
            $$"""{"sources":[{"source":"linear","phase":"live","usable":false,"complete":false,"lastSync":null,"lastCode":null,"pull":"running","records":0,"lastCommit":null,"needsLook":true,"lookWords":{{lookWords}}}]}""");
        var card = new ContextIndexCardViewModel(_server, _clock);
        await card.Refresh(_api, Token);
        return card;
    }

    [Fact]
    public async Task A_source_the_server_flags_with_words_shows_those_words_as_its_line()
    {
        var card = await Read("\"" + Words + "\"");

        Assert.Equal([Words + " · 0 records · nothing committed yet"], card.Lines.Select(l => l.Text), StringComparer.Ordinal);
        Assert.Equal("needs a look", card.Word, StringComparer.Ordinal);
        Assert.Equal(StateGlyph.Warning, card.Glyph);
    }

    [Fact]
    public async Task A_source_not_flagged_draws_the_ok_mark()
    {
        _server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK,
            """{"sources":[{"source":"linear","phase":"live","usable":true,"complete":true,"lastSync":null,"lastCode":null,"pull":"idle","records":0,"lastCommit":null,"needsLook":false,"lookWords":null}]}""");
        var card = new ContextIndexCardViewModel(_server, _clock);
        await card.Refresh(_api, Token);

        Assert.Equal(("ok", StateGlyph.Ok), (card.Word, card.Glyph));
    }

    [Fact]
    public async Task An_unreadable_status_draws_no_mark()
    {
        _server.IndexStatusAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-API-500", "broken");
        var card = new ContextIndexCardViewModel(_server, _clock);
        await card.Refresh(_api, Token);

        Assert.Equal((string.Empty, StateGlyph.None), (card.Word, card.Glyph));
    }

    [Fact]
    public async Task A_source_flagged_with_no_words_reads_its_state_as_before()
    {
        var card = await Read("null");

        Assert.Equal(["linear: pulling · 0 records · nothing committed yet"], card.Lines.Select(l => l.Text), StringComparer.Ordinal);
        Assert.Equal("needs a look", card.Word, StringComparer.Ordinal);
    }
}
