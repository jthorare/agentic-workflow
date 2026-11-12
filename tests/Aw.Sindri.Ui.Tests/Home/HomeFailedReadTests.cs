using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Queue;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Home;

/// <summary>#3056 (audit #3019): a failed queue read on Home is a failed line with Retry, never an empty "Nothing running."; stale and stalled marks get Retry too.</summary>
[Lane(TestLane.Unit)]
public sealed class HomeFailedReadTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<QueueViewModel> Queue()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        return new QueueViewModel(_server, catalog, _clock);
    }

    [Fact]
    public async Task A_failed_first_queue_read_shows_a_failed_line_in_plain_words_and_no_empty_sentence()
    {
        _server.QueueView = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)");
        var queue = await Queue();

        await queue.Refresh(_api, Token);

        var failure = Assert.IsType<FailedRead>(queue.ReadFailure);
        Assert.Equal((Labels.QueueReadFailed, "SND-API-011", "Retry", false), (failure.Words, failure.Code, failure.RetryText, failure.HasFix));
        Assert.DoesNotContain("SND-", failure.Words, StringComparison.Ordinal);
        Assert.Equal((string.Empty, string.Empty), (queue.RunningEmpty, queue.QueuedEmpty));
    }

    [Fact]
    public async Task Retry_on_the_failed_line_reads_the_queue_again_and_the_line_goes_away()
    {
        _server.QueueView = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "behind");
        var queue = await Queue();
        await queue.Refresh(_api, Token);
        var failure = Assert.IsType<FailedRead>(queue.ReadFailure);

        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting");
        await failure.Retry(Token);

        Assert.Equal((null, false, 5), (queue.ReadFailure, queue.HasProblem, queue.Total));
    }

    [Fact]
    public async Task Stale_rows_get_a_line_with_retry_that_clears_when_the_read_works()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting");
        var queue = await Queue();
        await queue.Refresh(_api, Token);
        Assert.Null(queue.StaleRead);

        _server.QueueView = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "behind");
        await queue.Refresh(_api, Token);
        var stale = Assert.IsType<FailedRead>(queue.StaleRead);
        Assert.Equal((Labels.RowsStaleNote, "Retry"), (stale.Words, stale.RetryText));

        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting");
        await stale.Retry(Token);
        Assert.Equal((false, null), (queue.IsStale, queue.StaleRead));
    }

    [Fact]
    public void A_stalled_needs_you_list_says_so_for_the_retry_beside_its_banner()
    {
        var list = new NeedsYouViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _clock);

        list.Update(new NeedsYouFeed([], "x") { Stalled = true });

        Assert.Equal((true, Labels.RowsStaleNote), (list.IsStalled, list.Banner));
    }
}
