using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Worktrees;
using Aw.Meta.Features.WorktreeSweep;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>
/// Issue 2753: a /bugFixOrchestrator run's red tree and candidate tree sit on branches that never get a pull request or reach
/// origin/main (the PR comes from a third branch), so every sweep kept them forever. Once the run's GitHub issue is closed by a merged
/// pull request, a full sweep removes every tree the run names at its recorded commit; the live-tree rules still keep one.
/// </summary>
public sealed class FinishedBugfixTreesSweptTests
{
    private const string Red = "/work/.claude/worktrees/ui-r2689";
    private const string Cand = "/work/.claude/worktrees/ui-c2689";
    private const string RedCommit = "cb20bc941d";
    private const string CandCommit = "71c4b9a62c";
    private const int Issue = 2689;
    private readonly MetaWorld _world = new();

    private static string Stamp(int minutes) => A.At(minutes).ToString("O", CultureInfo.InvariantCulture);

    // What `gh api graphql` answers about the issue: its state and its closing pull requests.
    private static string IssueAnswer(string state, params (int Number, string State)[] closers) =>
        "{\"data\":{\"repository\":{\"issue\":{\"state\":\"" + state + "\",\"closedByPullRequestsReferences\":{\"nodes\":["
        + string.Join(',', closers.Select(static c => "{\"number\":" + c.Number.ToString(CultureInfo.InvariantCulture) + ",\"state\":\"" + c.State + "\"}"))
        + "]}}}}}";

    // The red tree on its own branch (pushed), the candidate on another (one unpushed commit); neither has a PR or is in origin/main.
    private void Script(string issueAnswer, int issueExit = 0, string redHead = RedCommit) => _ = _world.Runner
        .When("git fetch", string.Empty)
        .When("rev-parse --verify --quiet origin/main", "mainsha\n")
        .When("worktree list --porcelain", "worktree /work\nHEAD aaaaaaa\nbranch refs/heads/main\n\n"
            + "worktree " + Red + "\nHEAD " + redHead + "\nbranch refs/heads/bugfix/gh-2689-red\n\n"
            + "worktree " + Cand + "\nHEAD " + CandCommit + "\nbranch refs/heads/bugfix/gh-2689-cand\n")
        .When("gh pr list", "[]")
        .When("api graphql", issueAnswer, issueExit)
        .When("aaaaaaa --not origin/main", "0\n")
        .When("--not origin/main", "1\n")
        .When(CandCommit + " --not --remotes", "1\n")
        .When("--not --remotes", "0\n")
        .When("status --porcelain", string.Empty)
        .When("rev-parse --show-toplevel", request => new CommandResult(0, request.Cwd + "\n", string.Empty, TimedOut: false))
        .When("worktree remove", string.Empty)
        .When("worktree prune", string.Empty);

    // Run gh-2689 resolved by c1: its red tree from the baseline evidence and its winning candidate.
    private void Records(int? issue = Issue)
    {
        _world.BugfixRecords.Add(new BugfixCandidate("gh-2689", "red", Red, string.Empty, RedCommit, Evaluated: true, Resolves: false, Finished: true, ResolvedBy: "c1") { Issue = issue, Baseline = true });
        _world.BugfixRecords.Add(new BugfixCandidate("gh-2689", "c1", Cand, "bugfix/gh-2689-cand", CandCommit, Evaluated: true, Resolves: true, Finished: true, ResolvedBy: "c1") { Issue = issue });
    }

    private async Task<SweepWorktreesResponse> Sweep(string issueAnswer, int issueExit = 0, string redHead = RedCommit, int? issue = Issue)
    {
        Script(issueAnswer, issueExit, redHead);
        Records(issue);
        _ = await _world.Values.Set(KvKeys.WorktreesSweepStart, Stamp(60), CancellationToken.None);
        _world.WorktreeFiles.Touch(Red, A.At(10));
        _world.WorktreeFiles.Touch(Cand, A.At(10));
        await _world.Ready();
        var service = new SweepWorktreesService(_world.Config, _world.Sweeper);
        var result = await service.Handle(new SweepWorktreesRequest(DryRun: false), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static bool Removed(SweepWorktreesResponse swept, string path) => swept.Removed.Any(r => string.Equals(r.Path, path, StringComparison.Ordinal));

    private static WorktreeReport Kept(SweepWorktreesResponse swept, string path) => Assert.Single(swept.Kept, r => string.Equals(r.Path, path, StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public async Task A_full_sweep_removes_the_red_and_candidate_trees_once_the_issue_is_closed_by_a_merged_pr()
    {
        var swept = await Sweep(IssueAnswer("CLOSED", (2720, "MERGED")));

        Assert.True(Removed(swept, Red), string.Join(" | ", swept.Kept.SelectMany(static k => k.Reasons)));
        Assert.True(Removed(swept, Cand), string.Join(" | ", swept.Kept.SelectMany(static k => k.Reasons)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Both_trees_are_kept_while_the_issue_is_open()
    {
        var swept = await Sweep(IssueAnswer("OPEN"));

        Assert.Equal(WorktreeDecision.Kept, Kept(swept, Red).Decision);
        Assert.Equal(WorktreeDecision.Kept, Kept(swept, Cand).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Both_trees_are_kept_while_the_closing_pr_is_still_open()
    {
        var swept = await Sweep(IssueAnswer("OPEN", (2720, "OPEN")));

        Assert.Equal(WorktreeDecision.Kept, Kept(swept, Red).Decision);
        Assert.Equal(WorktreeDecision.Kept, Kept(swept, Cand).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Both_trees_are_kept_when_the_issue_was_closed_without_a_merged_pr()
    {
        var swept = await Sweep(IssueAnswer("CLOSED", (2720, "CLOSED")));

        Assert.Equal(WorktreeDecision.Kept, Kept(swept, Red).Decision);
        Assert.Equal(WorktreeDecision.Kept, Kept(swept, Cand).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tree_whose_head_moved_past_the_recorded_commit_is_kept()
    {
        var swept = await Sweep(IssueAnswer("CLOSED", (2720, "MERGED")), redHead: "eeeeeee");

        Assert.Equal(WorktreeDecision.Kept, Kept(swept, Red).Decision);
        Assert.True(Removed(swept, Cand));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_whose_ticket_is_not_a_github_issue_keeps_its_trees()
    {
        var swept = await Sweep(IssueAnswer("CLOSED", (2720, "MERGED")), issue: null);

        Assert.Equal(WorktreeDecision.Kept, Kept(swept, Red).Decision);
        Assert.Equal(WorktreeDecision.Kept, Kept(swept, Cand).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_issue_read_keeps_both_trees()
    {
        var swept = await Sweep(string.Empty, issueExit: 1);

        Assert.Equal(WorktreeDecision.Kept, Kept(swept, Red).Decision);
        Assert.Equal(WorktreeDecision.Kept, Kept(swept, Cand).Decision);
    }
}
