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
        Assert.Contains(kept.Reasons, r => r.Contains("changed since the last sweep", StringComparison.Ordinal));
    }
}
