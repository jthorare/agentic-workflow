using Aw.Kernel.Testing;

namespace Aw.Meta.Tests.Cli;

/// <summary>
/// Issue 2293 over real git: `worktree-sweep --path P --done` removes a detached leftover tree (not a gate tree) whose HEAD is
/// in origin/main or on a pushed branch. Every other keep rule still holds, and a full sweep must have recorded its start first.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class WorktreeDoneDetachedTests : IDisposable
{
    private readonly WorktreeLab _lab = new();

    private async Task<(string OnMain, string OnPushed, string Unpushed)> Box()
    {
        await _lab.Init();
        var onMain = Path.Combine(_lab.Root, "api-e2e");
        WorktreeLab.Git(_lab.Repo, "worktree", "add", "--quiet", "--detach", onMain, "origin/main");

        var pushedCommit = await _lab.Branch("pushed-branch");
        var onPushed = Path.Combine(_lab.Root, "api-1205b");
        WorktreeLab.Git(_lab.Repo, "worktree", "add", "--quiet", "--detach", onPushed, pushedCommit);

        var unpushed = Path.Combine(_lab.Root, "api-mainprobe");
        WorktreeLab.Git(_lab.Repo, "worktree", "add", "--quiet", "--detach", unpushed, "origin/main");
        await WorktreeLab.CommitLocally(unpushed, "probe.txt");

        await _lab.Gh();
        await _lab.Team();
        return (onMain, onPushed, unpushed);
    }

    // A full sweep first records its start (rule 7 needs one); the clock is later than the trees' files.
    private async Task<CliResult> Sweep(params string[] args)
    {
        _lab.Cli.Clock.SetUtcNow(RealTime.NowOffset.AddHours(1));
        _ = await _lab.Cli.Run("worktree-sweep");
        return await _lab.Cli.Run(["worktree-sweep", .. args]);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_detached_tree_on_origin_main_is_removed()
    {
        var (onMain, _, _) = await Box();

        var sweep = await Sweep("--path", onMain, "--done");

        Assert.Equal(0, sweep.Exit);
        Assert.False(Directory.Exists(onMain), sweep.Out);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_detached_tree_on_a_pushed_branch_commit_is_removed_and_the_branch_stays()
    {
        var (_, onPushed, _) = await Box();

        var sweep = await Sweep("--path", onPushed, "--done");

        Assert.False(Directory.Exists(onPushed), sweep.Out);
        Assert.NotEmpty(WorktreeLab.GitOut(_lab.Repo, "rev-parse", "--verify", "pushed-branch"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_done_a_detached_tree_is_kept()
    {
        var (onMain, _, _) = await Box();

        var sweep = await Sweep("--path", onMain);

        Assert.True(Directory.Exists(onMain), sweep.Out);
        Assert.Contains("detached, not a gate tree", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_detached_tree_with_an_unpushed_commit_is_kept()
    {
        var (_, _, unpushed) = await Box();

        var sweep = await Sweep("--path", unpushed, "--done");

        Assert.True(Directory.Exists(unpushed), sweep.Out);
        Assert.Contains("commit(s) are on no remote branch", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_dirty_detached_tree_is_kept()
    {
        var (onMain, _, _) = await Box();
        await File.WriteAllTextAsync(Path.Combine(onMain, "NOTES.md"), "notes", TestContext.Current.CancellationToken);

        var sweep = await Sweep("--path", onMain, "--done");

        Assert.True(Directory.Exists(onMain), sweep.Out);
        Assert.Contains("NOTES.md", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_in_use_detached_tree_is_kept()
    {
        var (onMain, _, _) = await Box();
        _lab.StartSleeper(onMain);

        var sweep = await Sweep("--path", onMain, "--done");

        Assert.True(Directory.Exists(onMain), sweep.Out);
        Assert.Contains("in use by pid", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_locked_detached_tree_is_kept()
    {
        var (onMain, _, _) = await Box();
        WorktreeLab.Git(_lab.Repo, "worktree", "lock", onMain);

        var sweep = await Sweep("--path", onMain, "--done");

        Assert.True(Directory.Exists(onMain), sweep.Out);
        Assert.Contains("locked", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_a_previous_sweep_the_tree_is_kept()
    {
        var (onMain, _, _) = await Box();

        _lab.Cli.Clock.SetUtcNow(RealTime.NowOffset.AddHours(1));
        var sweep = await _lab.Cli.Run("worktree-sweep", "--path", onMain, "--done");

        Assert.True(Directory.Exists(onMain), sweep.Out);
        Assert.Contains("no previous sweep to compare with", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tree_on_a_branch_is_kept_by_done()
    {
        await _lab.Init();
        _ = await _lab.Branch("feature-x");
        var tree = _lab.Tree("feature-x");
        await _lab.Gh();
        await _lab.Team();

        var sweep = await Sweep("--path", tree, "--done", "--include-no-pr");

        Assert.True(Directory.Exists(tree), sweep.Out);
        Assert.Contains("--done: the tree is on a branch", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Done_without_path_is_a_usage_error()
    {
        await _lab.Init();
        await _lab.Gh();
        await _lab.Team();

        var sweep = await _lab.Cli.Run("worktree-sweep", "--done");

        Assert.Equal(2, sweep.Exit);
        Assert.Contains("--done needs --path", sweep.Out + sweep.Err, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Done_together_with_discarded_is_a_usage_error()
    {
        await _lab.Init();
        await _lab.Gh();
        await _lab.Team();

        var sweep = await _lab.Cli.Run("worktree-sweep", "--path", _lab.Root, "--done", "--discarded");

        Assert.Equal(2, sweep.Exit);
        Assert.Contains("--done and --discarded can't be combined", sweep.Out + sweep.Err, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _lab.StopSleepers();
        _lab.Dispose();
    }
}
