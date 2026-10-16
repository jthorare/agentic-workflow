using System.Text.Json;

namespace Aw.Meta.Tests.Cli;

/// <summary>
/// The 2026-10-16 incident and its relatives over real git: a worktree whose head is reachable from origin/main is not thereby finished.
/// Only an ended pull request says so.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class WorktreeUnfinishedWorkTests : IDisposable
{
    private readonly WorktreeLab _lab = new();

    private async Task<(string Fresh, string Reachable, string Detached)> Box()
    {
        await _lab.Init();

        // The incident: a branch created from origin/main a moment ago, no commits of its own, no pull request.
        var fresh = Path.Combine(_lab.Root, "ideas-scoping");
        WorktreeLab.Git(_lab.Repo, "worktree", "add", "--quiet", "-b", "docs/scoping-station", fresh, "origin/main");

        // Commits and an ancestor of origin/main through another merge, but still no pull request.
        _ = await _lab.Branch("reachable-nopr");
        _lab.MergeIntoMain("reachable-nopr");
        var reachable = _lab.Tree("reachable-nopr");

        // A detached worktree on a commit of origin/main that is not a gate tree.
        var detached = Path.Combine(_lab.Root, "detached");
        WorktreeLab.Git(_lab.Repo, "worktree", "add", "--quiet", "--detach", detached, "origin/main");

        await _lab.Gh();
        await _lab.Team();
        return (fresh, reachable, detached);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_fresh_branch_with_no_commits_and_no_pull_request_survives_a_sweep()
    {
        var (fresh, _, _) = await Box();

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.Equal(0, sweep.Exit);
        Assert.True(Directory.Exists(fresh), sweep.Out);
        Assert.Contains("no pull request", sweep.Out, StringComparison.Ordinal);
        Assert.Contains("fresh branch, no commits", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_branch_with_commits_reachable_through_another_merge_but_no_pull_request_survives()
    {
        var (_, reachable, _) = await Box();

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.True(Directory.Exists(reachable), sweep.Out);
        Assert.Contains("no pull request: work not finished", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_detached_worktree_that_is_not_a_gate_tree_survives()
    {
        var (_, _, detached) = await Box();

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.True(Directory.Exists(detached), sweep.Out);
        Assert.Contains("detached, not a gate tree", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tree_changed_after_the_previous_sweep_survives_even_with_a_merged_pull_request()
    {
        await _lab.Init();
        _ = await _lab.Branch("done", pr: 1);
        _lab.MergeIntoMain("done");
        await _lab.Gh((1, "MERGED", "done"));
        await _lab.Team();
        _lab.Cli.Clock.SetUtcNow(DateTimeOffset.UtcNow.AddHours(-1));
        _ = await _lab.Cli.Run("worktree-sweep");   // records its start an hour ago
        var tree = _lab.Tree("done");   // made after that sweep started

        var sweep = await _lab.Cli.Run("worktree-sweep", "--json");

        Assert.True(Directory.Exists(tree), sweep.Out);
        using var document = JsonDocument.Parse(sweep.Out);
        Assert.Contains("changed since the last sweep", document.RootElement.GetProperty("kept").ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_merged_clean_idle_tree_that_has_not_changed_since_the_previous_sweep_is_removed()
    {
        await _lab.Init();
        _ = await _lab.Branch("done", pr: 1);
        _lab.MergeIntoMain("done");
        var tree = _lab.Tree("done");
        await _lab.Gh((1, "MERGED", "done"));
        await _lab.Team();
        _lab.Cli.Clock.SetUtcNow(DateTimeOffset.UtcNow.AddHours(1));
        _lab.StartSleeper(tree);
        _ = await _lab.Cli.Run("worktree-sweep");   // kept (in use); records its start, after the tree was made
        Assert.True(Directory.Exists(tree));
        _lab.StopSleepers();

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.False(Directory.Exists(tree), sweep.Out);
    }

    public void Dispose() => _lab.Dispose();
}
