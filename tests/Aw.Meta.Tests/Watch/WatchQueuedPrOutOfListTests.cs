using Aw.Meta.Core;
using Aw.Meta.Features.Status;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Watch;

/// <summary>#3738: the poll lists only the 50 newest pull requests, so a queued pull request that merged and scrolled out of that list never reached watch.ended-prs and stayed in the merge queue for good.</summary>
public sealed class WatchQueuedPrOutOfListTests
{
    private readonly MetaWorld _world = new();

    private WatchService Service() => new(
        _world.Config,
        new WatchPoller(_world.Repo, _world.Resources, _world.InboxCounts, _world.Values, FollowUps(), Worlds.Reservation(_world), _world.Context),
        _world.Values,
        _world.Delay,
        _world.Clock,
        new LaneReport(_world.Monitor, _world.Values),
        _world.Board);

    // With the team inputs, as DI builds it: without them the poll reads no ready messages and checks nothing outside the listing.
    private PollFollowUps FollowUps() => new(
        new WatchSweeper(_world.Config, _world.Sweeper, _world.Values),
        Worlds.QueueWatch(_world),
        new TeamPrInputs(_world.Repo, _world.Values, _world.Messages, _world.Paths, _world.WorktreeFiles));

    private StatusService Status() => new(_world.Config, Worlds.StatusReader(_world), _world.Values, _world.Resources, Worlds.Advisor(_world), _world.Processes, _world.Clock);

    // One poll: the listing holds only pull request 300; #250 and #251 were said ready and are not in it.
    private async Task PollOnce(string state250, string state251)
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "ready #250 079c2541");
        await _world.Receive("m2", "t1-ui", "ready #251 079c2542");
        _ = _world.Runner
            .When("gh pr list", """[{"number":300,"state":"OPEN","isDraft":false,"headRefOid":"aaa1111","mergeable":"MERGEABLE","title":"t300"}]""")
            .When("git ls-remote origin main", "aaaaaaa1111111\trefs/heads/main\n")
            .When("gh pr view 250 --json state", "{\"state\":\"" + state250 + "\"}")
            .When("gh pr view 251 --json state", "{\"state\":\"" + state251 + "\"}");
        _ = await Service().Handle(new WatchRequest(TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(120), _ => ValueTask.CompletedTask), CancellationToken.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_pr_that_merged_and_left_the_listing_leaves_the_merge_queue()
    {
        await PollOnce("MERGED", "OPEN");

        var status = await Status().Handle(new StatusRequest(), CancellationToken.None);

        Assert.Equal([251], status.Value.MergeQueue.Select(q => q.Pr));
        Assert.Equal("250", (await _world.Values.Get(KvKeys.EndedPrs, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_pr_that_closed_and_left_the_listing_leaves_the_merge_queue()
    {
        await PollOnce("OPEN", "CLOSED");

        var status = await Status().Handle(new StatusRequest(), CancellationToken.None);

        Assert.Equal([250], status.Value.MergeQueue.Select(q => q.Pr));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_pr_that_is_open_and_out_of_the_listing_stays_queued()
    {
        await PollOnce("OPEN", "OPEN");

        var status = await Status().Handle(new StatusRequest(), CancellationToken.None);

        Assert.Equal([250, 251], status.Value.MergeQueue.Select(q => q.Pr));
    }
}
