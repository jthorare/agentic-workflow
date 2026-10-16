using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Worktrees;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Worktrees;

public sealed class SweepRecordsStartTests
{
    private readonly MetaWorld _world = new();

    private void Script() => _ = _world.Runner
        .When("git fetch", string.Empty)
        .When("rev-parse --verify --quiet origin/main", "mainsha\n")
        .When("worktree list --porcelain", "worktree /work\nHEAD aaaaaaa\nbranch refs/heads/main\n")
        .When("gh pr list", "[]");

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
}
