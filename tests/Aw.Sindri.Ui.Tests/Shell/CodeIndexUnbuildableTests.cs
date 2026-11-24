using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Health's Code index card for a repo whose first index can never build (#3187): the server's sentence when it sends one, else "can't build", never "first build queued".</summary>
[Lane(TestLane.Unit)]
public sealed class CodeIndexUnbuildableTests
{
    private const string Words = "demo: can't build a code index: no default branch to build from (no origin/HEAD, main or master); check out the default branch or fetch origin";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<CodeIndexCardViewModel> Read(bool needsLook, string lookWords)
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK,
            $$"""{"repos":[{"repo":"demo","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[{"repo":"demo","state":"unbuildable","embeddingsPercent":null,"waiting":null,"paused":false,"needsLook":{{(needsLook ? "true" : "false")}},"lookWords":{{lookWords}}}]}""");
        var card = new CodeIndexCardViewModel(_server, _clock);
        await card.Refresh(_api, Token);
        return card;
    }

    [Fact]
    public async Task The_servers_sentence_is_the_line_and_the_card_needs_a_look()
    {
        var card = await Read(needsLook: true, "\"" + Words + "\"");

        Assert.Equal(Words, Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task With_no_sentence_the_line_reads_cant_build_not_first_build_queued()
        => Assert.Equal("demo: can't build", Assert.Single((await Read(needsLook: false, "null")).Lines).Text);
}
