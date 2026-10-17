namespace Aw.Meta.Tests.Cli;

/// <summary>The triggers: merge-run sweeps the merged pull request's worktrees (and its gate trees); the watch sweeps a pull request it sees merged or closed. Never a timer.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class WorktreeTriggerTests : IDisposable
{
    private readonly WorktreeLab _lab = new();
    private string _feat = string.Empty;
    private string _other = string.Empty;

    // Pull request 7 (branch feat) is open and gated, with a worktree; pull request 8 (branch other) is open and unrelated, with one too.
    private async Task Open(bool enabled = true)
    {
        await _lab.Init();
        var head = await _lab.Branch("feat", pr: 7);
        _ = await _lab.Branch("other", pr: 8);
        _feat = _lab.Tree("feat");
        _other = _lab.Tree("other");
        await _lab.Team(enabled, roots: [_lab.Root]);
        await _lab.Gh((7, "OPEN", "feat"), (8, "OPEN", "other"));
        // `gh pr merge 7` lands the head on main (a fast-forward) and flips the pull request to MERGED, as GitHub would.
        await File.WriteAllTextAsync(
            Path.Combine(_lab.GhState, "merge-7.sh"),
            $"git -C '{_lab.Repo}' push -q origin {head}:refs/heads/main && sed -i.bak 's/\"state\":\"OPEN\"/\"state\":\"MERGED\"/' '{_lab.GhState}/view-7.json' && sed -i.bak 's/\"number\":7,\"state\":\"OPEN\"/\"number\":7,\"state\":\"MERGED\"/' '{_lab.GhState}/list.json'\n",
            TestContext.Current.CancellationToken);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Merging_a_pull_request_removes_its_worktree_and_its_gate_tree_and_logs_it()
    {
        await Open();
        Assert.Equal(0, (await _lab.Cli.Run("gate-run", "7")).Exit);
        var gateTree = Path.Combine(_lab.Cli.File("meta"), "gates", "7", "tree");
        Assert.True(Directory.Exists(gateTree));

        var merge = await _lab.Cli.Run("merge-run", "7");

        Assert.Equal(0, merge.Exit);
        Assert.True(merge.Out.Contains("MERGED #7", StringComparison.Ordinal), merge.Out + merge.Err);
        Assert.True(merge.Out.Contains("WORKTREES merge-run #7: removed 2", StringComparison.Ordinal), merge.Out + merge.Err);
        Assert.False(Directory.Exists(_feat));
        Assert.False(Directory.Exists(gateTree));
        Assert.True(Directory.Exists(_other), "another pull request's worktree is not this trigger's business");
        var log = await File.ReadAllTextAsync(Path.Combine(_lab.Cli.File("meta"), "log.md"), TestContext.Current.CancellationToken);
        Assert.Contains("NOTE", log, StringComparison.Ordinal);
        Assert.Contains("worktrees merge-run #7: removed 2", log, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Merging_keeps_a_worktree_that_has_work_or_a_process_in_it_and_says_why()
    {
        await Open();
        await File.WriteAllTextAsync(Path.Combine(_feat, "NOTES.md"), "wip", TestContext.Current.CancellationToken);
        _ = await _lab.Cli.Run("gate-run", "7");

        var merge = await _lab.Cli.Run("merge-run", "7");

        Assert.Equal(0, merge.Exit);
        Assert.True(Directory.Exists(_feat));
        Assert.True(merge.Out.Contains("kept 1", StringComparison.Ordinal), merge.Out + merge.Err);
        Assert.True(merge.Out.Contains("NOTES.md", StringComparison.Ordinal), merge.Out + merge.Err);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task With_the_triggers_disabled_a_merge_only_reports_what_it_would_remove()
    {
        await Open(enabled: false);
        _ = await _lab.Cli.Run("gate-run", "7");

        var merge = await _lab.Cli.Run("merge-run", "7");

        Assert.Equal(0, merge.Exit);
        Assert.True(merge.Out.Contains("would remove", StringComparison.Ordinal), merge.Out + merge.Err);
        Assert.True(Directory.Exists(_feat));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_merge_still_succeeds_when_the_sweep_cannot_run()
    {
        await Open();
        _ = await _lab.Cli.Run("gate-run", "7");
        _lab.StartSleeper(_feat);
        _lab.Cli.SetEnv("AW_CLAUDE_SESSIONS_DIR", _lab.Cli.File("sessions"));

        var merge = await _lab.Cli.Run("merge-run", "7");

        Assert.Equal(0, merge.Exit);
        Assert.True(merge.Out.Contains("MERGED #7", StringComparison.Ordinal), merge.Out + merge.Err);
        Assert.True(Directory.Exists(_feat), "a process is in it");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_watch_sweeps_a_pull_request_it_sees_merged()
    {
        await Open();
        _lab.MergeIntoMain("feat");
        await _lab.Gh((7, "MERGED", "feat"), (8, "OPEN", "other"));

        var watch = await _lab.Cli.Run("watch", "--interval", "5", "--for", "5");

        Assert.Equal(0, watch.Exit);
        Assert.True(watch.Out.Contains("WORKTREES watch #7: removed 1", StringComparison.Ordinal), watch.Out + watch.Err);
        Assert.False(Directory.Exists(_feat));
        Assert.True(Directory.Exists(_other));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_watch_sweeps_a_pull_request_it_sees_closed_when_nothing_is_unpushed()
    {
        await Open();
        await _lab.Gh((7, "OPEN", "feat"), (8, "CLOSED", "other"));

        var watch = await _lab.Cli.Run("watch", "--interval", "5", "--for", "5");

        Assert.True(watch.Out.Contains("WORKTREES watch #8: removed 1", StringComparison.Ordinal), watch.Out + watch.Err);
        Assert.False(Directory.Exists(_other));
        Assert.True(Directory.Exists(_feat));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_watch_looks_at_a_pull_request_once_not_on_every_poll()
    {
        await Open();
        _lab.MergeIntoMain("feat");
        await _lab.Gh((7, "MERGED", "feat"), (8, "OPEN", "other"));
        _ = await _lab.Cli.Run("watch", "--interval", "5", "--for", "5");
        var again = await _lab.Cli.Run("watch", "--interval", "5", "--for", "5");

        Assert.False(again.Out.Contains("WORKTREES", StringComparison.Ordinal), again.Out + again.Err);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Idle_open_work_is_never_removed_however_long_it_sits()
    {
        await Open();
        _ = await _lab.Cli.Run("gate-run", "8");   // PR 8 is open: gated, idle, clean, pushed and unmerged

        var watch = await _lab.Cli.Run("watch", "--interval", "5", "--for", "5");
        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.False(watch.Out.Contains("WORKTREES", StringComparison.Ordinal), watch.Out + watch.Err);
        Assert.Equal(0, sweep.Exit);
        Assert.True(Directory.Exists(_feat));
        Assert.True(Directory.Exists(_other));
    }

    public void Dispose() => _lab.Dispose();
}
