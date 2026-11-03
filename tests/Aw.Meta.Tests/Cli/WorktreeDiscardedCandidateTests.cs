using System.Text.Json;
using Aw.Kernel.Testing;

namespace Aw.Meta.Tests.Cli;

/// <summary>
/// Issue 2005 over real git: `worktree-sweep --path P --discarded` removes a losing /bugFixOrchestrator candidate tree, whose fix
/// commit is never pushed, when the run's state.json under $AW_STATE_DIR/bugfix names it at its HEAD as evaluated and not resolving,
/// and the run is over or another candidate won. Every other rule still holds, and the branch is never deleted.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class WorktreeDiscardedCandidateTests : IDisposable
{
    private readonly WorktreeLab _lab = new();

    private sealed record Candidate(string Id, string Worktree, string Branch, string Commit, bool Evaluated, bool Resolves);

    // A losing candidate (c1, never pushed) and the winner (c2) of bug run gh-9, each in its own tree.
    private async Task<(string Loser, string LoserCommit, string Winner, string WinnerCommit)> Box()
    {
        await _lab.Init();
        var loserCommit = await _lab.Branch("bugfix/gh-9-a1", push: false);
        var loser = _lab.Tree("bugfix/gh-9-a1");
        var winnerCommit = await _lab.Branch("bugfix/gh-9-a2", push: false);
        var winner = _lab.Tree("bugfix/gh-9-a2");
        await _lab.Gh();
        await _lab.Team();
        return (loser, loserCommit, winner, winnerCommit);
    }

    private async Task State(bool finished, string? resolvedBy, params Candidate[] candidates)
    {
        var path = _lab.Cli.File("bugfix/owner-repo/gh-9/state.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var rows = candidates.Select(static c => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["id"] = c.Id,
            ["worktree"] = c.Worktree,
            ["branch"] = c.Branch,
            ["commit"] = c.Commit,
            ["evaluated"] = c.Evaluated,
            ["resolves"] = c.Resolves,
        });
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 1,
            ["finished"] = finished,
            ["resolvedBy"] = resolvedBy,
            ["candidates"] = rows.ToList(),
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(state), TestContext.Current.CancellationToken);
    }

    private async Task<CliResult> Sweep(params string[] args)
    {
        _lab.Cli.Clock.SetUtcNow(RealTime.NowOffset.AddHours(1));   // the recorded start must be later than the trees' files
        return await _lab.Cli.Run(["worktree-sweep", .. args]);
    }

    private async Task LostToC2(string loser, string loserCommit, string winner, string winnerCommit) =>
        await State(finished: true, resolvedBy: "c2", new Candidate("c1", loser, "bugfix/gh-9-a1", loserCommit, Evaluated: true, Resolves: false), new Candidate("c2", winner, "bugfix/gh-9-a2", winnerCommit, Evaluated: true, Resolves: true));

    [Fact, Trait("Lane", "integration")]
    public async Task A_losing_candidate_is_removed_and_its_branch_stays()
    {
        var (loser, loserCommit, winner, winnerCommit) = await Box();
        await LostToC2(loser, loserCommit, winner, winnerCommit);

        var sweep = await Sweep("--path", loser, "--discarded");

        Assert.Equal(0, sweep.Exit);
        Assert.False(Directory.Exists(loser), sweep.Out);
        Assert.Equal(loserCommit, WorktreeLab.GitOut(_lab.Repo, "rev-parse", "--verify", "bugfix/gh-9-a1"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Path_limits_the_sweep_to_that_tree()
    {
        var (loser, loserCommit, winner, winnerCommit) = await Box();
        await LostToC2(loser, loserCommit, winner, winnerCommit);

        var sweep = await Sweep("--path", loser, "--discarded");

        Assert.True(Directory.Exists(winner), sweep.Out);
        Assert.DoesNotContain(winner, sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_discarded_the_losing_candidate_is_kept_for_unfinished_work()
    {
        var (loser, loserCommit, winner, winnerCommit) = await Box();
        await LostToC2(loser, loserCommit, winner, winnerCommit);

        var sweep = await Sweep("--path", loser);

        Assert.True(Directory.Exists(loser), sweep.Out);
        Assert.Contains("work not finished", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_winning_candidate_is_kept()
    {
        var (loser, loserCommit, winner, winnerCommit) = await Box();
        await LostToC2(loser, loserCommit, winner, winnerCommit);

        var sweep = await Sweep("--path", winner, "--discarded");

        Assert.True(Directory.Exists(winner), sweep.Out);
        Assert.Contains("--discarded: candidate c2 resolved the bug", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_loser_with_a_commit_on_top_of_the_evaluated_one_is_kept()
    {
        var (loser, loserCommit, winner, winnerCommit) = await Box();
        await LostToC2(loser, loserCommit, winner, winnerCommit);
        await WorktreeLab.CommitLocally(loser, "more.txt");

        var sweep = await Sweep("--path", loser, "--discarded");

        Assert.True(Directory.Exists(loser), sweep.Out);
        Assert.Contains("--discarded: HEAD moved past the evaluated commit", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_loser_of_a_run_that_is_not_over_is_kept()
    {
        var (loser, loserCommit, winner, winnerCommit) = await Box();
        await State(finished: false, resolvedBy: null, new Candidate("c1", loser, "bugfix/gh-9-a1", loserCommit, Evaluated: true, Resolves: false), new Candidate("c2", winner, "bugfix/gh-9-a2", winnerCommit, Evaluated: false, Resolves: false));

        var sweep = await Sweep("--path", loser, "--discarded");

        Assert.True(Directory.Exists(loser), sweep.Out);
        Assert.Contains("--discarded: the bugfix run is not over", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_loser_of_an_unfinished_run_that_another_candidate_resolved_is_removed()
    {
        var (loser, loserCommit, winner, winnerCommit) = await Box();
        await State(finished: false, resolvedBy: "c2", new Candidate("c1", loser, "bugfix/gh-9-a1", loserCommit, Evaluated: true, Resolves: false), new Candidate("c2", winner, "bugfix/gh-9-a2", winnerCommit, Evaluated: true, Resolves: true));

        var sweep = await Sweep("--path", loser, "--discarded");

        Assert.False(Directory.Exists(loser), sweep.Out);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_candidate_that_was_never_evaluated_is_kept()
    {
        var (loser, loserCommit, winner, winnerCommit) = await Box();
        await State(finished: true, resolvedBy: "c2", new Candidate("c1", loser, "bugfix/gh-9-a1", loserCommit, Evaluated: false, Resolves: false), new Candidate("c2", winner, "bugfix/gh-9-a2", winnerCommit, Evaluated: true, Resolves: true));

        var sweep = await Sweep("--path", loser, "--discarded");

        Assert.True(Directory.Exists(loser), sweep.Out);
        Assert.Contains("--discarded: candidate c1 was never evaluated", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tree_no_bugfix_run_names_is_kept()
    {
        var (loser, _, _, _) = await Box();

        var sweep = await Sweep("--path", loser, "--discarded");

        Assert.True(Directory.Exists(loser), sweep.Out);
        Assert.Contains("--discarded: no bugfix run names this worktree", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_dirty_losing_candidate_is_kept()
    {
        var (loser, loserCommit, winner, winnerCommit) = await Box();
        await LostToC2(loser, loserCommit, winner, winnerCommit);
        await File.WriteAllTextAsync(Path.Combine(loser, "NOTES.md"), "notes", TestContext.Current.CancellationToken);

        var sweep = await Sweep("--path", loser, "--discarded");

        Assert.True(Directory.Exists(loser), sweep.Out);
        Assert.Contains("NOTES.md", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_candidate_recorded_on_another_branch_is_kept_with_that_reason()
    {
        var (loser, loserCommit, winner, winnerCommit) = await Box();
        await State(finished: true, resolvedBy: "c2", new Candidate("c1", loser, "bugfix/gh-9-other", loserCommit, Evaluated: true, Resolves: false), new Candidate("c2", winner, "bugfix/gh-9-a2", winnerCommit, Evaluated: true, Resolves: true));

        var sweep = await Sweep("--path", loser, "--discarded");

        Assert.True(Directory.Exists(loser), sweep.Out);
        Assert.Contains("the tree is on bugfix/gh-9-a1, the candidate was on bugfix/gh-9-other", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_path_sweep_does_not_shorten_rule_7_for_other_trees()
    {
        var (loser, loserCommit, winner, winnerCommit) = await Box();
        await LostToC2(loser, loserCommit, winner, winnerCommit);
        _lab.Cli.Clock.SetUtcNow(RealTime.NowOffset.AddHours(-1));
        _ = await _lab.Cli.Run("worktree-sweep");   // records its start an hour ago
        var fresh = Path.Combine(_lab.Root, "ideas-scoping");   // cut from origin/main after that sweep started
        WorktreeLab.Git(_lab.Repo, "worktree", "add", "--quiet", "-b", "docs/scoping-station", fresh, "origin/main");

        var path = await Sweep("--path", loser, "--discarded");
        var full = await _lab.Cli.Run("worktree-sweep", "--include-no-pr");

        Assert.False(Directory.Exists(loser), path.Out);
        Assert.True(Directory.Exists(fresh), full.Out);
        Assert.Contains("changed since the last sweep", full.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Discarded_without_path_is_a_usage_error()
    {
        await _lab.Init();
        await _lab.Gh();
        await _lab.Team();

        var sweep = await _lab.Cli.Run("worktree-sweep", "--discarded");

        Assert.Equal(2, sweep.Exit);
        Assert.Contains("--discarded needs --path", sweep.Out + sweep.Err, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _lab.StopSleepers();
        _lab.Dispose();
    }
}
