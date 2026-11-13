using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The System page's index cards say "needs a look" from the server's needsLook flag, not from a list of state words (#3430):
/// over every state the server serves, and one the app has never seen, the card's word follows the flag.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class NeedsLookContractTests
{
    private const string Sha = "abcdef0123456789abcdef0123456789abcdef01";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string, bool> CodeIndexCases()
    {
        var data = new TheoryData<string, bool>();
        foreach (var state in new[] { "ok", "missing", "damaged", "stale", "building", "waiting", "queued", "warming-up" })   // the last one: a state this app has never seen
        {
            foreach (var flag in new[] { true, false })
            {
                data.Add(state, flag);
            }
        }

        return data;
    }

    public static TheoryData<string, bool> ContextCases()
    {
        var data = new TheoryData<string, bool>();
        foreach (var pull in new[] { "idle", "running", "backoff", "failed", "paused" })   // the last one: a word this app has never seen
        {
            foreach (var flag in new[] { true, false })
            {
                data.Add(pull, flag);
            }
        }

        return data;
    }

    private static string Flag(bool needsLook) => needsLook ? "true" : "false";

    private static string Expected(bool needsLook) => needsLook ? Labels.CodeIndexNeedsLook : Labels.CodeIndexOk;

    [Theory]
    [MemberData(nameof(CodeIndexCases))]
    public async Task The_code_index_card_word_follows_needsLook_for_every_health_state(string state, bool needsLook)
    {
        var repo = $$"""{"repo":"repo","state":"ok","commit":"{{Sha}}","builtAt":"{{_clock.GetUtcNow().AddMinutes(-3).ToString("O", CultureInfo.InvariantCulture)}}","layers":[],"counts":null}""";
        var health = $$"""{"repo":"repo","state":"{{state}}","embeddingsPercent":null,"waiting":null,"needsLook":{{Flag(needsLook)}}}""";
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"repos":[{{repo}}],"health":[{{health}}]}""");
        var card = new CodeIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(Expected(needsLook), card.Word);
    }

    [Fact]
    public async Task A_missing_index_is_fine_because_doctor_does_not_count_it_as_a_problem()
    {
        var repo = """{"repo":"repo","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null}""";
        var health = """{"repo":"repo","state":"missing","embeddingsPercent":null,"waiting":null,"needsLook":false}""";
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"repos":[{{repo}}],"health":[{{health}}]}""");
        var card = new CodeIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(Labels.CodeIndexOk, card.Word);   // pinned on purpose (#3430): before the flag a missing repo read "needs a look"
    }

    [Theory]
    [MemberData(nameof(ContextCases))]
    public async Task The_context_card_word_follows_needsLook_for_every_pull_state(string pull, bool needsLook)
    {
        _server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK,
            $$"""{"sources":[{"source":"github","phase":"live","usable":true,"complete":false,"lastSync":null,"lastCode":null,"pull":"{{pull}}","records":5,"lastCommit":null,"needsLook":{{Flag(needsLook)}}}]}""");
        var card = new ContextIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(Expected(needsLook), card.Word);
    }
}
