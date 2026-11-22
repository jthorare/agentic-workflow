using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// #3430: the Code index and Context index cards say "needs a look" exactly when the server flags a row with <c>needsLook</c>, whatever state word
/// comes with it, a word this app has never seen included. The app keeps no list of state words that count as a look.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class NeedsLookFollowsServerTests
{
    private const string Sha = "abcdef0123456789abcdef0123456789abcdef01";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Flag(bool value) => value ? "true" : "false";

    private string Repo(string state, string graph = "ok", string? graphCode = null) =>
        $$"""{"repo":"repo","state":"{{state}}","commit":"{{Sha}}","builtAt":"{{_clock.GetUtcNow().AddMinutes(-3):O}}","layers":[{"layer":"structure","state":"ok","code":null,"detail":"ok"},{"layer":"graph","state":"{{graph}}","code":{{(graphCode is null ? "null" : "\"" + graphCode + "\"")}},"detail":"ok"}],"counts":null}""";

    private static string Health(string state, bool needsLook, bool paused = false) =>
        $$"""{"repo":"repo","state":"{{state}}","embeddingsPercent":null,"waiting":null,"paused":{{Flag(paused)}},"needsLook":{{Flag(needsLook)}},"lookWords":null}""";

    private async Task<CodeIndexCardViewModel> Code(string repo, string health)
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"repos":[{{repo}}],"health":[{{health}}]}""");
        var card = new CodeIndexCardViewModel(_server, _clock);
        await card.Refresh(_api, Token);
        return card;
    }

    // A source the old app rule read as a look on every pull word it knew: nothing usable, nothing held, nothing committed, a code on the last attempt.
    private static string Source(string? pull, bool needsLook) =>
        $$"""{"source":"slack","phase":"live","usable":false,"complete":false,"lastSync":null,"lastCode":"SND-CONTEXT-041","pull":{{(pull is null ? "null" : "\"" + pull + "\"")}},"records":0,"lastCommit":null,"lastError":null,"lastErrorText":null,"failures":0,"nextTry":null,"needsLook":{{Flag(needsLook)}}}""";

    private async Task<ContextIndexCardViewModel> Context(params string[] sources)
    {
        _server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"sources":[{{string.Join(',', sources)}}]}""");
        var card = new ContextIndexCardViewModel(_server, _clock);
        await card.Refresh(_api, Token);
        return card;
    }

    [Theory]
    [InlineData("ok", true)]
    [InlineData("ok", false)]
    [InlineData("missing", true)]
    [InlineData("missing", false)]
    [InlineData("damaged", true)]
    [InlineData("damaged", false)]
    [InlineData("stale", true)]
    [InlineData("stale", false)]
    [InlineData("building", true)]
    [InlineData("building", false)]
    [InlineData("queued", true)]
    [InlineData("queued", false)]
    [InlineData("waiting", true)]
    [InlineData("waiting", false)]
    [InlineData("something-new", true)]
    [InlineData("something-new", false)]
    public async Task The_code_index_card_needs_a_look_exactly_when_the_server_flags_the_repo(string state, bool needsLook)
    {
        var card = await Code(Repo(state), Health(state, needsLook));

        Assert.Equal(needsLook, string.Equals(card.Word, Labels.CodeIndexNeedsLook, StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_unavailable_graph_the_server_does_not_flag_is_not_a_look()
    {
        // The old app rule read any layer outside ok, disabled and pending as a look; the server's rule does not count this one.
        var card = await Code(Repo("ok", graph: "unavailable"), Health("ok", needsLook: false));

        Assert.Equal(Labels.CodeIndexOk, card.Word);
    }

    [Fact]
    public async Task A_fine_repo_the_server_flags_is_a_look()
    {
        var card = await Code(Repo("ok"), Health("ok", needsLook: true));

        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task A_build_running_reads_building_not_needs_a_look()
    {
        var card = await Code(Repo("ok"), Health("building", needsLook: false));

        Assert.Equal(Labels.CodeIndexState("building"), card.Word);
    }

    [Fact]
    public async Task A_repo_with_no_index_while_builds_are_paused_is_not_a_look()
    {
        var card = await Code(Repo("missing"), Health("missing", needsLook: false, paused: true));

        Assert.Equal(Labels.CodeIndexOk, card.Word);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(null, false)]
    [InlineData("idle", true)]
    [InlineData("idle", false)]
    [InlineData("running", true)]
    [InlineData("running", false)]
    [InlineData("backoff", true)]
    [InlineData("backoff", false)]
    [InlineData("failed", true)]
    [InlineData("failed", false)]
    [InlineData("throttled", true)]
    [InlineData("throttled", false)]
    public async Task The_context_index_card_needs_a_look_exactly_when_the_server_flags_a_source(string? pull, bool needsLook)
    {
        var card = await Context(Source(pull, needsLook));

        Assert.Equal(needsLook, string.Equals(card.Word, Labels.CodeIndexNeedsLook, StringComparison.Ordinal));
    }

    [Fact]
    public async Task One_flagged_source_beside_fine_ones_makes_the_context_index_card_a_look()
    {
        var card = await Context(Source("running", needsLook: false), Source("idle", needsLook: true));

        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }
}
