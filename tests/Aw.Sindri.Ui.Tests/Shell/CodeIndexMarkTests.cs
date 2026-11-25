using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The Code index card's mark beside its word (#4331), as the Context index card's (#4129): the check for "ok", the attention mark for "needs a look",
/// none for building, build queued, no repo in scope and an unreadable status, so those leave no gap before the word.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class CodeIndexMarkTests
{
    private const string Sha = "abcdef0123456789abcdef0123456789abcdef01";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Status(string state, string healthState, bool needsLook) =>
        $$"""{"repos":[{"repo":"repo","state":"{{state}}","commit":"{{Sha}}","builtAt":null,"layers":[],"counts":null}],"health":[{"repo":"repo","state":"{{healthState}}","embeddingsPercent":null,"waiting":null,"needsLook":{{(needsLook ? "true" : "false")}}}]}""";

    private async Task<CodeIndexCardViewModel> Read(RouteAnswer answer)
    {
        _server.CodeIndexStatusAnswer = answer;
        var card = new CodeIndexCardViewModel(_server, _clock);
        await card.Refresh(_api, Token);
        return card;
    }

    private Task<CodeIndexCardViewModel> Read(string body) => Read(new RouteAnswer(HttpStatusCode.OK, body));

    [Fact]
    public async Task Ok_draws_the_ok_mark()
    {
        var card = await Read(Status("ok", "ok", needsLook: false));

        Assert.Equal((Labels.CodeIndexOk, StateGlyph.Ok, true), (card.Word, card.Glyph, card.HasGlyph));
    }

    [Fact]
    public async Task Needs_a_look_draws_the_attention_mark()
    {
        var card = await Read(Status("ok", "stale", needsLook: true));

        Assert.Equal((Labels.CodeIndexNeedsLook, StateGlyph.Warning, true), (card.Word, card.Glyph, card.HasGlyph));
    }

    [Theory]
    [InlineData("building", Labels.CodeIndexBuilding)]
    [InlineData("queued", Labels.CodeIndexQueued)]
    public async Task Work_in_progress_draws_no_mark(string healthState, string word)
    {
        var card = await Read(Status("missing", healthState, needsLook: false));

        Assert.Equal((word, StateGlyph.None, false), (card.Word, card.Glyph, card.HasGlyph));
    }

    [Fact]
    public async Task No_repo_in_scope_draws_no_mark()
    {
        var card = await Read("""{"repos":[],"health":[]}""");

        Assert.Equal((string.Empty, StateGlyph.None, false), (card.Word, card.Glyph, card.HasGlyph));
    }

    [Fact]
    public async Task An_unreadable_status_draws_no_mark()
    {
        var card = await Read(RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-API-500", "broken"));

        Assert.Equal((string.Empty, StateGlyph.None, false), (card.Word, card.Glyph, card.HasGlyph));
    }
}
