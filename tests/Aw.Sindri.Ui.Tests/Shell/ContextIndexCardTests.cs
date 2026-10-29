using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Health's Context index card (#1597): one line per source, so a source that is idle with nothing in it can't hide behind another that is pulling.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextIndexCardTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Linear_at_zero_while_github_pulls_reads_nothing_committed_and_needs_a_look()
    {
        var commit = _clock.GetUtcNow().AddMinutes(-3);
        _server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK,
            $$"""{"sources":[{"source":"github","phase":"live","usable":true,"complete":false,"lastSync":null,"lastCode":null,"pull":"running","records":200,"lastCommit":"{{commit:O}}"},{"source":"linear","phase":"live","usable":false,"complete":false,"lastSync":null,"lastCode":null,"pull":"idle","records":0,"lastCommit":null}]}""");
        var card = new ContextIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(["github: pulling · 200 records · last commit 3 min ago", "linear: idle · 0 records · nothing committed yet"], card.Lines.Select(l => l.Text), StringComparer.Ordinal);
        Assert.Equal("needs a look", card.Word, StringComparer.Ordinal);
    }
}
