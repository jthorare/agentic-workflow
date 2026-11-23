using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>A source on its first pass (phase recent or backfill) reads as progress on the Context index card, not as a bare pull word (#4035).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextIndexFirstSyncTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_source_on_its_first_pass_reads_first_sync_with_the_records_so_far_and_the_card_is_ok()
    {
        var commit = _clock.GetUtcNow().AddMinutes(-1);
        _server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK,
            $$"""{"sources":[{"source":"linear","phase":"backfill","usable":true,"complete":false,"lastSync":"{{commit:O}}","lastCode":"SND-CONTEXT-059","pull":"idle","records":12400,"lastCommit":"{{commit:O}}","needsLook":false},{"source":"slack","phase":"recent","usable":false,"complete":false,"lastSync":null,"lastCode":null,"pull":"running","records":0,"lastCommit":null,"needsLook":false}]}""");
        var card = new ContextIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(["linear: first sync, 12400 records so far · last commit 1 min ago", "slack: first sync, 0 records so far · nothing committed yet"], card.Lines.Select(l => l.Text), StringComparer.Ordinal);
        Assert.Equal(Labels.CodeIndexOk, card.Word);
    }
}
