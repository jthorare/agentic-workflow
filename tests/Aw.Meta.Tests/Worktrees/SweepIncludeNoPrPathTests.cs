using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Features.WorktreeSweep;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>#2284: worktree-sweep --include-no-pr --path DIR removes a pushed, clean tree with no pull request, as it does without --path.</summary>
public sealed class SweepIncludeNoPrPathTests
{
    private const string Loose = "/work/.claude/worktrees/loose";
    private readonly MetaWorld _world = new();

    private static string Stamp(int minutes) => A.At(minutes).ToString("O", CultureInfo.InvariantCulture);

    private void Script() => _ = _world.Runner
        .When("git fetch", string.Empty)
        .When("rev-parse --verify --quiet origin/main", "mainsha\n")
        .When("worktree list --porcelain", "worktree /work\nHEAD aaaaaaa\nbranch refs/heads/main\n\nworktree " + Loose + "\nHEAD ddddddd\nbranch refs/heads/loose\n")
        .When("gh pr list", "[]")
        .When("--not origin/main", "0\n")
        .When("--not --remotes", "0\n")
        .When("status --porcelain", string.Empty)
        .When("rev-parse --show-toplevel", request => new CommandResult(0, request.Cwd + "\n", string.Empty, TimedOut: false))
        .When("worktree remove", string.Empty)
        .When("worktree prune", string.Empty);

    private async Task<SweepWorktreesResponse> Sweep(bool includeNoPr, int touchedAt = 10)
    {
        Script();
        _ = await _world.Values.Set(KvKeys.WorktreesSweepStart, Stamp(60), CancellationToken.None);
        _world.WorktreeFiles.Touch(Loose, A.At(touchedAt));
        await _world.Ready();
        var service = new SweepWorktreesService(_world.Config, _world.Sweeper);
        var result = await service.Handle(new SweepWorktreesRequest(DryRun: false, Pr: null, IncludeNoPr: includeNoPr, Path: Loose), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_include_no_pr_a_pushed_clean_no_pr_tree_is_removed_by_path()
    {
        var swept = await Sweep(includeNoPr: true);

        Assert.Contains(swept.Removed, r => string.Equals(r.Path, Loose, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_include_no_pr_the_same_tree_is_kept_for_no_pr()
    {
        var swept = await Sweep(includeNoPr: false);

        var kept = Assert.Single(swept.Kept, r => string.Equals(r.Path, Loose, StringComparison.Ordinal));
        Assert.Contains(kept.Reasons, r => r.Contains("kept: no PR", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_include_no_pr_a_tree_changed_since_the_last_sweep_is_still_kept()
    {
        var swept = await Sweep(includeNoPr: true, touchedAt: 100);

        var kept = Assert.Single(swept.Kept, r => string.Equals(r.Path, Loose, StringComparison.Ordinal));
        Assert.Contains(kept.Reasons, r => r.Contains("changed since the last full sweep", StringComparison.Ordinal));
    }

    /// <summary>#3350: only a full sweep records the baseline, so a --path sweep on a fresh state dir kept the tree with "no previous sweep" on every run.</summary>
    [Fact, Trait("Lane", "unit")]
    public async Task Path_sweep_removes_the_tree_on_its_second_run_without_a_full_sweep()
    {
        Script();
        _world.WorktreeFiles.Touch(Loose, A.At(10));
        await _world.Ready();
        var service = new SweepWorktreesService(_world.Config, _world.Sweeper);
        var request = new SweepWorktreesRequest(DryRun: false, Pr: null, IncludeNoPr: true, Path: Loose);

        var first = await service.Handle(request, CancellationToken.None);
        Assert.True(first.IsSuccess);
        var second = await service.Handle(request, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Contains(second.Value.Removed, r => string.Equals(r.Path, Loose, StringComparison.Ordinal));
    }

    /// <summary>#3350: the baseline key uses the normalized path, so a trailing slash on the first run does not hide it from the second.</summary>
    [Fact, Trait("Lane", "unit")]
    public async Task A_path_sweep_finds_its_baseline_when_the_path_is_spelled_with_a_trailing_slash()
    {
        Script();
        _world.WorktreeFiles.Touch(Loose, A.At(10));
        await _world.Ready();
        var service = new SweepWorktreesService(_world.Config, _world.Sweeper);

        var first = await service.Handle(new SweepWorktreesRequest(DryRun: false, Pr: null, IncludeNoPr: true, Path: Loose + "/"), CancellationToken.None);
        Assert.True(first.IsSuccess);
        var second = await service.Handle(new SweepWorktreesRequest(DryRun: false, Pr: null, IncludeNoPr: true, Path: Loose), CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Contains(second.Value.Removed, r => string.Equals(r.Path, Loose, StringComparison.Ordinal));
    }

    /// <summary>#3350: the per-tree marker never replaces the global one; a full sweep after a --path sweep still compares with the global marker.</summary>
    [Fact, Trait("Lane", "unit")]
    public async Task A_full_sweep_after_a_path_sweep_still_uses_the_global_marker()
    {
        Script();
        _ = await _world.Values.Set(KvKeys.WorktreesSweepStart, Stamp(5), CancellationToken.None);
        _world.WorktreeFiles.Touch(Loose, A.At(10));
        await _world.Ready();
        var service = new SweepWorktreesService(_world.Config, _world.Sweeper);

        var byPath = await service.Handle(new SweepWorktreesRequest(DryRun: false, Pr: null, IncludeNoPr: true, Path: Loose), CancellationToken.None);
        Assert.True(byPath.IsSuccess);
        var full = await service.Handle(new SweepWorktreesRequest(DryRun: true, Pr: null, IncludeNoPr: true, Path: null), CancellationToken.None);

        Assert.True(full.IsSuccess);
        var kept = Assert.Single(full.Value.Kept, r => string.Equals(r.Path, Loose, StringComparison.Ordinal));
        Assert.Contains(kept.Reasons, r => r.Contains("changed since the last full sweep", StringComparison.Ordinal));
        Assert.Equal(Stamp(5), await _world.Values.Get(KvKeys.WorktreesSweepStart, CancellationToken.None) is { IsSuccess: true } g ? g.Value : null);
    }
}
