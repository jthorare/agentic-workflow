using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Queue;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>The Work toolbar (F3 spec work §2, §7): All and Running with their counts, the text filter over id, title and repo, and the repo chip.</summary>
[Lane(TestLane.Unit)]
public sealed class QueueFilterTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ItemSummaryView Item(string id, string title, string repo) =>
        new(id, title, repo, "running", "plan", A.Noon.AddMinutes(-5), PrimaryAction: null, Actions: []);

    private async Task<QueueViewModel> Queue()
    {
        var view = new QueueView(
            [
                new QueueGroupView("running", [Item("ITM-1", "Try the queue", "alpha"), Item("ITM-2", "Fix the lock", "beta")], "Nothing is running."),
                new QueueGroupView("queued", [Item("ITM-3", "Write docs", "alpha")], "Nothing is queued."),
            ],
            "Nothing in the queue yet.",
            new AsOfView(1, 1, A.Noon, Rebuilding: false, Stalled: false));
        _server.QueueView = new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(view, SindriApiClient.Json));
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var queue = new QueueViewModel(_server, catalog, _clock);
        await queue.Refresh(_api, Token);
        return queue;
    }

    [Fact]
    public async Task The_segments_carry_the_counts_of_all_and_of_running()
    {
        var queue = await Queue();

        Assert.Equal((Labels.QueueAllSegment(3), Labels.QueueRunningSegment(2)), (queue.AllSegment, queue.RunningSegment));
    }

    [Fact]
    public async Task The_running_segment_shows_only_the_running_group()
    {
        var queue = await Queue();

        queue.Filter(QueueFilter.Running);

        Assert.Equal(["running"], queue.Groups.Where(g => g.IsVisible).Select(g => g.Group), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_text_filter_matches_id_title_and_repo()
    {
        var queue = await Queue();

        queue.FilterText = "docs";
        Assert.Equal(["ITM-3"], queue.Groups.SelectMany(g => g.Rows).Select(r => r.Id), StringComparer.Ordinal);
        queue.FilterText = "itm-2";
        Assert.Equal(["ITM-2"], queue.Groups.SelectMany(g => g.Rows).Select(r => r.Id), StringComparer.Ordinal);
        queue.FilterText = "beta";
        Assert.Equal(["ITM-2"], queue.Groups.SelectMany(g => g.Rows).Select(r => r.Id), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_repo_chip_lists_the_repos_served_and_narrows_the_rows()
    {
        var queue = await Queue();

        Assert.Equal([Labels.AllRepos, "alpha", "beta"], queue.RepoChoices, StringComparer.Ordinal);
        queue.ChooseRepo("alpha");
        Assert.Equal(["ITM-1", "ITM-3"], queue.Groups.SelectMany(g => g.Rows).Select(r => r.Id), StringComparer.Ordinal);
        queue.ChooseRepo(Labels.AllRepos);
        Assert.Equal(3, queue.Groups.SelectMany(g => g.Rows).Count());
    }
}
