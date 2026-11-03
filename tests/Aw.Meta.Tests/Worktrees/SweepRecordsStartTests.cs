using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Worktrees;
using Aw.Meta.Features.MergeRun;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Worktrees;

public sealed class SweepRecordsStartTests
{
    private readonly MetaWorld _world = new();

    private void Script() => _ = _world.Runner
        .When("git fetch", string.Empty)
        .When("rev-parse --verify --quiet origin/main", "mainsha\n")
        .When("worktree list --porcelain", "worktree /work\nHEAD aaaaaaa\nbranch refs/heads/main\n")
        .When("gh pr list", "[]")
        .When("gh pr view", string.Empty, exitCode: 1);

    // #2011: rule 7 compares every tree against this marker, so only a sweep that judged every tree may move it.
    private const string Earlier = "2026-01-01T00:00:00.0000000+00:00";

    private async Task<string?> MarkerAfter(Func<Task> sweep)
    {
        Script();
        _ = await _world.Values.Set(KvKeys.WorktreesSweepStart, Earlier, CancellationToken.None);
        await sweep();
        return (await _world.Values.Get(KvKeys.WorktreesSweepStart, CancellationToken.None)).Value;
    }

    private sealed class MergedRun : IService<MergeRunRequest, MergeRunResponse>
    {
        public ValueTask<Result<MergeRunResponse>> Handle(MergeRunRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success(new MergeRunResponse(request.Pr, Merged: true, [], "sha", "announce")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_removing_sweep_records_when_it_started()
    {
        Script();
        var start = _world.Clock.GetUtcNow();

        var result = await _world.Sweeper.Sweep(new TeamConfig("t", "/work", ["api"], "meta"), WorktreeScope.All, SweepMode.Remove, "sweep", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var recorded = await _world.Values.Get(KvKeys.WorktreesSweepStart, CancellationToken.None);
        Assert.Equal(start, DateTimeOffset.Parse(recorded.Value!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dry_run_leaves_the_recorded_start_alone()
    {
        Script();

        _ = await _world.Sweeper.Sweep(new TeamConfig("t", "/work", ["api"], "meta"), WorktreeScope.All, SweepMode.DryRun, "sweep", CancellationToken.None);

        Assert.Null((await _world.Values.Get(KvKeys.WorktreesSweepStart, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_sweep_that_could_not_judge_anything_records_nothing()
    {
        _ = _world.Runner.When("git fetch", string.Empty, exitCode: 1);

        var result = await _world.Sweeper.Sweep(new TeamConfig("t", "/work", ["api"], "meta"), WorktreeScope.All, SweepMode.Remove, "sweep", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Null((await _world.Values.Get(KvKeys.WorktreesSweepStart, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_removing_sweep_of_one_pull_request_leaves_the_recorded_start_alone() =>
        Assert.Equal(Earlier, await MarkerAfter(async () =>
            _ = await _world.Sweeper.Sweep(new TeamConfig("t", "/work", ["api"], "meta"), WorktreeScope.ForPrs([7]), SweepMode.Remove, "sweep", CancellationToken.None)));

    [Fact, Trait("Lane", "unit")]
    public async Task A_removing_sweep_of_one_path_leaves_the_recorded_start_alone() =>
        Assert.Equal(Earlier, await MarkerAfter(async () =>
            _ = await _world.Sweeper.Sweep(new TeamConfig("t", "/work", ["api"], "meta"), WorktreeScope.ForPath("/work", discarded: false), SweepMode.Remove, "sweep", CancellationToken.None)));

    [Fact, Trait("Lane", "unit")]
    public async Task The_sweep_after_a_merge_leaves_the_recorded_start_alone()
    {
        await _world.Ready();

        Assert.Equal(Earlier, await MarkerAfter(async () =>
            _ = await new SweepAfterMergeService(new MergedRun(), _world.Config, _world.Sweeper).Handle(new MergeRunRequest(7), CancellationToken.None)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_watch_sweep_of_ended_pull_requests_leaves_the_recorded_start_alone()
    {
        await _world.Ready();

        Assert.Equal(Earlier, await MarkerAfter(async () =>
            _ = await new WatchSweeper(_world.Config, _world.Sweeper, _world.Values).Sweep([9], CancellationToken.None)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_removing_sweep_of_every_worktree_including_no_pr_ones_still_records_when_it_started()
    {
        var start = _world.Clock.GetUtcNow();

        var recorded = await MarkerAfter(async () =>
            _ = await _world.Sweeper.Sweep(new TeamConfig("t", "/work", ["api"], "meta"), WorktreeScope.AllIncludingNoPr, SweepMode.Remove, "sweep", CancellationToken.None));

        Assert.Equal(start, DateTimeOffset.Parse(recorded!, System.Globalization.CultureInfo.InvariantCulture));
    }
}
