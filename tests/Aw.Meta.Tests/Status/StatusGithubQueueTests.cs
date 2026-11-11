using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Merge;
using Aw.Meta.Features.Status;
using Aw.Meta.Testing;
using Aw.Meta.Tests.MergeRun;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Status;

public sealed class StatusGithubQueueTests
{
    private readonly MetaWorld _world = new();

    private StatusService Service() => new(_world.Config, Worlds.StatusReader(_world), _world.Values, _world.Resources, Worlds.Advisor(_world), _world.Processes, _world.Clock);

    private async Task Setup(string answer, bool fails = false)
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "ready #250 079c2541");
        await _world.Receive("m2", "t1-ui", "ready #251 079c2542");
        _ = await _world.Values.Set(KvKeys.MergeQueued, MergeQueued.With(stored: null, new QueuedMerge(250, _world.Clock.GetUtcNow(), "079c2541")), CancellationToken.None);
        _ = _world.Runner.When("gh api graphql", _ => new CommandResult(fails ? 1 : 0, fails ? string.Empty : answer, fails ? "HTTP 502" : string.Empty, TimedOut: false));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_in_githubs_merge_queue_shows_its_position_and_entry_state()
    {
        await Setup(QueueAnswers.Queued(2));

        var status = await Service().Handle(new StatusRequest(), CancellationToken.None);

        Assert.Equal(["queued 2 (AWAITING_CHECKS)", null], status.Value.MergeQueue.Select(q => q.Github), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_read_shows_the_pull_request_as_queued_unknown()
    {
        await Setup(QueueAnswers.Queued(2), fails: true);

        var status = await Service().Handle(new StatusRequest(), CancellationToken.None);

        Assert.Equal(["queued (unknown)", null], status.Value.MergeQueue.Select(q => q.Github), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_queued_pull_requests_cost_a_gh_call()
    {
        await Setup(QueueAnswers.Queued(2));

        _ = await Service().Handle(new StatusRequest(), CancellationToken.None);

        _ = Assert.Single(_world.Runner.Lines, l => l.StartsWith("gh api graphql", StringComparison.Ordinal));
    }
}
