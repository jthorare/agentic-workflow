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
        new LaneReport(_world.Monitor, _world.Values),
        _world.Board);

    // PR 190 was handed to the queue; the poll lists it in `state`, the queue read answers `queue` (or fails), and a state read after
    // the queue read answers `stateNow` (default: `state`; or fails).
    private async Task Queued(string state, string queue, bool queueFails = false, bool commitFails = false, bool titleFails = false, string? list = null, string? stateNow = null, bool stateFails = false)
    {
        await _world.Ready();
        _ = await _world.Values.Set(KvKeys.MergeQueued, MergeQueued.With(stored: null, new QueuedMerge(190, _world.Clock.GetUtcNow(), "aaa1111")), CancellationToken.None);
        list ??= $$"""[{"number":190,"state":"{{state}}","isDraft":false,"headRefOid":"aaa1111","mergeable":"MERGEABLE","title":"Add x"}]""";
        _ = _world.Runner
            .When("gh pr list", list)
            .When("git ls-remote origin main", "bbbbbbb2222222\trefs/heads/main\n")
            .When("gh api graphql", _ => Answer(queue, queueFails))
            .When("gh pr view 190 --json mergeCommit", _ => Answer("""{"mergeCommit":{"oid":"ddddddd4444444"}}""", commitFails))
            .When("gh pr view 191 --json mergeCommit", """{"mergeCommit":{"oid":"eeeeeee5555555"}}""")
            .When("gh pr view 190 --json state", _ => Answer($$"""{"state":"{{stateNow ?? state}}"}""", stateFails))
            .When("gh pr view 190", _ => Answer("""{"title":"Add x"}""", titleFails));
    }

    private static CommandResult Answer(string stdout, bool fails) =>
        fails ? new CommandResult(1, string.Empty, "HTTP 502", TimedOut: false) : new CommandResult(0, stdout, string.Empty, TimedOut: false);

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

        Assert.Equal(["MERGED #190 from GitHub's merge queue: main is now ddddddd"], QueueLines(), StringComparer.Ordinal);
        Assert.Equal("ddddddd4444444", (await _world.Gates.FindMerge(190, CancellationToken.None)).Value!.Sha);
        Assert.Contains(_world.Log.Lines, l => l.Contains("MERGE", StringComparison.Ordinal) && l.Contains("#190 Add x merged; main is now ddddddd", StringComparison.Ordinal));
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
        Assert.Contains(190, QueueDrops.Parse((await _world.Values.Get(KvKeys.QueueDropped, CancellationToken.None)).Value).Keys);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_the_queue_merged_after_the_poll_listed_it_open_is_recorded_as_merged_not_dropped()
    {
        await Queued("OPEN", QueueAnswers.OnNotQueued, stateNow: "MERGED");

        await Watch();

        Assert.Equal(["MERGED #190 from GitHub's merge queue: main is now ddddddd"], QueueLines(), StringComparer.Ordinal);
        Assert.Equal("ddddddd4444444", (await _world.Gates.FindMerge(190, CancellationToken.None)).Value!.Sha);
        Assert.Empty(await StillQueued());
        Assert.Null((await _world.Values.Get(KvKeys.QueueDropped, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_state_read_that_fails_after_the_queue_read_keeps_the_pull_request_for_the_next_poll()
    {
        await Queued("OPEN", QueueAnswers.OnNotQueued, stateFails: true);

        await Watch();

        Assert.Empty(QueueLines());
        Assert.Equal([190], await StillQueued());
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

    [Fact, Trait("Lane", "unit")]
    public async Task Two_queued_pull_requests_that_land_in_one_poll_each_get_their_own_merge_commit()
    {
        const string both = """[{"number":190,"state":"MERGED","isDraft":false,"headRefOid":"aaa1111","mergeable":"MERGEABLE","title":"Add x"},{"number":191,"state":"MERGED","isDraft":false,"headRefOid":"bbb2222","mergeable":"MERGEABLE","title":"Add y"}]""";
        await Queued("MERGED", QueueAnswers.OnNotQueued, list: both);
        _ = await _world.Values.Set(KvKeys.MergeQueued, MergeQueued.With(MergeQueued.With(stored: null, new QueuedMerge(190, _world.Clock.GetUtcNow(), "aaa1111")), new QueuedMerge(191, _world.Clock.GetUtcNow(), "bbb2222")), CancellationToken.None);

        await Watch();

        Assert.Equal("ddddddd4444444", (await _world.Gates.FindMerge(190, CancellationToken.None)).Value!.Sha);
        Assert.Equal("eeeeeee5555555", (await _world.Gates.FindMerge(191, CancellationToken.None)).Value!.Sha);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_commit_that_cannot_be_read_falls_back_to_mains_tip()
    {
        await Queued("MERGED", QueueAnswers.OnNotQueued, commitFails: true);

        await Watch();

        Assert.Equal("bbbbbbb2222222", (await _world.Gates.FindMerge(190, CancellationToken.None)).Value!.Sha);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_title_that_cannot_be_read_leaves_the_merge_log_line_without_a_gap()
    {
        await Queued("MERGED", QueueAnswers.OnNotQueued, titleFails: true);

        await Watch();

        Assert.Contains("#190 merged; main is now ddddddd", _world.Log.Lines.Single(l => l.Contains("MERGE", StringComparison.Ordinal)), StringComparison.Ordinal);
    }
}
