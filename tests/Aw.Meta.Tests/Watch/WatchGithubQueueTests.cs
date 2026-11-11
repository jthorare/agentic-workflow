using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;
using Aw.Meta.Tests.MergeRun;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Watch;

public sealed class WatchGithubQueueTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(120);
    private readonly MetaWorld _world = new();
    private readonly List<string> _lines = [];

    private WatchService Service() => new(
        _world.Config,
        new WatchPoller(_world.Repo, _world.Resources, _world.InboxCounts, _world.Values, Worlds.FollowUps(_world), Worlds.Reservation(_world), _world.Context),
        _world.Values,
        _world.Delay,
        _world.Clock,
        _world.Monitor,
        _world.Board);

    // PR 190 was handed to the queue; the poll lists it in `state`, and the queue read answers `queue` (or fails).
    private async Task Queued(string state, string queue, bool queueFails = false)
    {
        await _world.Ready();
        _ = await _world.Values.Set(KvKeys.MergeQueued, MergeQueued.With(stored: null, new QueuedMerge(190, _world.Clock.GetUtcNow(), "aaa1111")), CancellationToken.None);
        var list = $$"""[{"number":190,"state":"{{state}}","isDraft":false,"headRefOid":"aaa1111","mergeable":"MERGEABLE","title":"Add x"}]""";
        _ = _world.Runner
            .When("gh pr list", list)
            .When("git ls-remote origin main", "bbbbbbb2222222\trefs/heads/main\n")
            .When("gh api graphql", _ => new CommandResult(queueFails ? 1 : 0, queueFails ? string.Empty : queue, queueFails ? "HTTP 502" : string.Empty, TimedOut: false))
            .When("gh pr view 190", """{"title":"Add x"}""");
    }

    private async Task Watch()
    {
        var result = await Service().Handle(new WatchRequest(_interval, TimeSpan.FromSeconds(300), line => { _lines.Add(line); return ValueTask.CompletedTask; }), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
    }

    private IEnumerable<string> QueueLines() => _lines.Where(l => l.Contains("merge queue", StringComparison.Ordinal) || l.StartsWith("QUEUE OUT", StringComparison.Ordinal));

    private async Task<IReadOnlyCollection<int>> StillQueued() =>
        [.. MergeQueued.Parse((await _world.Values.Get(KvKeys.MergeQueued, CancellationToken.None)).Value).Keys];

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_pull_request_that_went_merged_is_recorded_logged_and_dropped_from_the_queued_set()
    {
        await Queued("MERGED", QueueAnswers.OnNotQueued);

        await Watch();

        Assert.Equal(["MERGED #190 from GitHub's merge queue: main is now bbbbbbb"], QueueLines(), StringComparer.Ordinal);
        Assert.Equal("bbbbbbb2222222", (await _world.Gates.FindMerge(190, CancellationToken.None)).Value!.Sha);
        Assert.Contains(_world.Log.Lines, l => l.Contains("MERGE", StringComparison.Ordinal) && l.Contains("#190 Add x merged; main is now bbbbbbb", StringComparison.Ordinal));
        Assert.Empty(await StillQueued());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_pull_request_that_is_merged_and_recorded_already_is_only_dropped_from_the_queued_set()
    {
        await Queued("MERGED", QueueAnswers.OnNotQueued);
        _ = await _world.Gates.RecordMerge(new MergeRecord(190, "ccccccc3333333", _world.Clock.GetUtcNow()), CancellationToken.None);

        await Watch();

        Assert.Empty(QueueLines());
        Assert.Equal("ccccccc3333333", (await _world.Gates.FindMerge(190, CancellationToken.None)).Value!.Sha);
        Assert.Empty(await StillQueued());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_open_pull_request_that_left_the_queue_says_why_and_is_dropped()
    {
        await Queued("OPEN", QueueAnswers.Removed("FAILED_CHECKS"));

        await Watch();

        Assert.Equal(["QUEUE OUT #190: FAILED_CHECKS; send the lane NOT OK"], QueueLines(), StringComparer.Ordinal);
        Assert.Empty(await StillQueued());
        Assert.Null((await _world.Gates.FindMerge(190, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_open_pull_request_that_left_the_queue_with_no_reason_says_it_left_the_queue()
    {
        await Queued("OPEN", QueueAnswers.OnNotQueued);

        await Watch();

        Assert.Equal(["QUEUE OUT #190: left GitHub's merge queue; send the lane NOT OK"], QueueLines(), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_open_pull_request_still_in_the_queue_stays_queued_and_says_nothing()
    {
        await Queued("OPEN", QueueAnswers.Queued(2));

        await Watch();

        Assert.Empty(QueueLines());
        Assert.Equal([190], await StillQueued());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_pull_request_that_was_closed_is_dropped_with_a_queue_out_line()
    {
        await Queued("CLOSED", QueueAnswers.OnNotQueued);

        await Watch();

        Assert.Equal(["QUEUE OUT #190: closed on GitHub; send the lane NOT OK"], QueueLines(), StringComparer.Ordinal);
        Assert.Empty(await StillQueued());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_queue_read_says_nothing_and_keeps_the_pull_request_for_the_next_poll()
    {
        await Queued("OPEN", QueueAnswers.Queued(1), queueFails: true);

        await Watch();

        Assert.Empty(QueueLines());
        Assert.Equal([190], await StillQueued());
    }
}
