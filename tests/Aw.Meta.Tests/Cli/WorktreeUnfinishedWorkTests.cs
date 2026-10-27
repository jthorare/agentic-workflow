using System.Text.Json;
using Aw.Kernel.Testing;

namespace Aw.Meta.Tests.Cli;

/// <summary>
/// The 2026-10-16 incident and its relatives over real git. A branch with no pull request is removed only when it is fully pushed (or in
/// origin/main), clean and idle, and not changed since the last sweep; those last rules are what protect a live tree on a fresh branch.
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
    public async Task No_pr_branch_with_head_in_origin_main_is_removed()
    {
        var (fresh, _, _) = await Box();

        _lab.Cli.Clock.SetUtcNow(RealTime.NowOffset.AddHours(1));   // the recorded start must be later than the trees' files
        var first = await _lab.Cli.Run("worktree-sweep");   // no previous sweep: kept, and it records its start
        Assert.Contains("no previous sweep to compare with", first.Out, StringComparison.Ordinal);
        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.Equal(0, sweep.Exit);
        Assert.False(Directory.Exists(fresh), sweep.Out);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_pr_branch_fully_pushed_clean_idle_is_removed()
    {
        var (_, reachable, _) = await Box();

        _lab.Cli.Clock.SetUtcNow(RealTime.NowOffset.AddHours(1));   // the recorded start must be later than the trees' files
        var first = await _lab.Cli.Run("worktree-sweep");   // no previous sweep: kept, and it records its start
        Assert.Contains("no previous sweep to compare with", first.Out, StringComparison.Ordinal);
        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.False(Directory.Exists(reachable), sweep.Out);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_pr_branch_is_kept_on_the_first_sweep()
    {
        var (fresh, reachable, _) = await Box();

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.True(Directory.Exists(fresh), sweep.Out);
        Assert.True(Directory.Exists(reachable), sweep.Out);
        Assert.Contains("no previous sweep to compare with", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_pr_branch_with_unpushed_commit_is_kept()
    {
        await _lab.Init();
        _ = await _lab.Branch("wip", push: false);
        var tree = _lab.Tree("wip");
        await _lab.Gh();
        await _lab.Team();

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.True(Directory.Exists(tree), sweep.Out);
        Assert.Contains("commit(s) are on no remote branch", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_pr_branch_dirty_is_kept()
    {
        var (fresh, _, _) = await Box();
        await File.WriteAllTextAsync(Path.Combine(fresh, "NOTES.md"), "notes", TestContext.Current.CancellationToken);

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.True(Directory.Exists(fresh), sweep.Out);
        Assert.Contains("NOTES.md", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_pr_branch_with_process_is_kept()
    {
        var (fresh, _, _) = await Box();
        _lab.StartSleeper(fresh);

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.True(Directory.Exists(fresh), sweep.Out);
        Assert.Contains("in use by pid", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_pr_branch_changed_since_last_sweep_is_kept()
    {
        await _lab.Init();
        await _lab.Gh();
        await _lab.Team();
        _lab.Cli.Clock.SetUtcNow(RealTime.NowOffset.AddHours(-1));
        _ = await _lab.Cli.Run("worktree-sweep");   // records its start an hour ago
        var fresh = Path.Combine(_lab.Root, "ideas-scoping");   // the incident: cut from origin/main after that sweep started
        WorktreeLab.Git(_lab.Repo, "worktree", "add", "--quiet", "-b", "docs/scoping-station", fresh, "origin/main");

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.True(Directory.Exists(fresh), sweep.Out);
        Assert.Contains("changed since the last sweep", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Open_pr_branch_is_still_kept()
    {
        await _lab.Init();
        _ = await _lab.Branch("open", pr: 3);
        var tree = _lab.Tree("open");
        await _lab.Gh((3, "OPEN", "open"));
        await _lab.Team();

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.True(Directory.Exists(tree), sweep.Out);
        Assert.Contains("still open", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Pr_filter_sweep_ignores_no_pr_branches()
    {
        var (fresh, reachable, _) = await Box();

        var sweep = await _lab.Cli.Run("worktree-sweep", "--pr", "99");

        Assert.True(Directory.Exists(fresh), sweep.Out);
        Assert.True(Directory.Exists(reachable), sweep.Out);
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
        _lab.Cli.Clock.SetUtcNow(RealTime.NowOffset.AddHours(-1));
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
        _lab.Cli.Clock.SetUtcNow(RealTime.NowOffset.AddHours(1));
        _lab.StartSleeper(tree);
        _ = await _lab.Cli.Run("worktree-sweep");   // kept (in use); records its start, after the tree was made
        Assert.True(Directory.Exists(tree));
        _lab.StopSleepers();

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.False(Directory.Exists(tree), sweep.Out);
    }

    public void Dispose() => _lab.Dispose();
}
