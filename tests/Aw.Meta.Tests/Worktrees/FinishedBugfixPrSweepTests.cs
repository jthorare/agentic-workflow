using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;
using Aw.Meta.Features.WorktreeSweep;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>Issue 2753: the `--pr N` sweep merge-run starts after a merge also takes the trees of the bugfix runs whose issue PR N closed, so they go in the same step.</summary>
public sealed class FinishedBugfixPrSweepTests
{
    private const string Red = "/work/.claude/worktrees/ui-r2689";
    private const string Cand = "/work/.claude/worktrees/ui-c2689";
    private const string RedCommit = "cb20bc941d";
    private const string CandCommit = "71c4b9a62c";
    private const string Closed = "{\"data\":{\"repository\":{\"issue\":{\"state\":\"CLOSED\",\"closedByPullRequestsReferences\":{\"nodes\":[{\"number\":2720,\"state\":\"MERGED\"}]}}}}}";
    private readonly MetaWorld _world = new();

    private async Task<SweepWorktreesResponse> Sweep(int pr)
    {
        _ = _world.Runner
            .When("git fetch", string.Empty)
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("worktree list --porcelain", "worktree /work\nHEAD aaaaaaa\nbranch refs/heads/main\n\n"
                + "worktree " + Red + "\nHEAD " + RedCommit + "\nbranch refs/heads/bugfix/gh-2689-red\n\n"
                + "worktree " + Cand + "\nHEAD " + CandCommit + "\nbranch refs/heads/bugfix/gh-2689-cand\n")
            .When("gh pr list", "[]")
            .When("pr view", string.Empty, exitCode: 1)
            .When("api graphql", Closed)
            .When("--not origin/main", "1\n")
            .When("--not --remotes", "0\n")
            .When("status --porcelain", string.Empty)
            .When("rev-parse --show-toplevel", request => new CommandResult(0, request.Cwd + "\n", string.Empty, TimedOut: false))
            .When("worktree remove", string.Empty)
            .When("worktree prune", string.Empty);
        _world.BugfixRecords.Add(new BugfixCandidate("gh-2689", "red", Red, string.Empty, RedCommit, Evaluated: true, Resolves: false, Finished: true, ResolvedBy: "c1") { Issue = 2689, Baseline = true });
        _world.BugfixRecords.Add(new BugfixCandidate("gh-2689", "c1", Cand, "bugfix/gh-2689-cand", CandCommit, Evaluated: true, Resolves: true, Finished: true, ResolvedBy: "c1") { Issue = 2689 });
        _ = await _world.Values.Set(KvKeys.WorktreesSweepStart, A.At(60).ToString("O", CultureInfo.InvariantCulture), CancellationToken.None);
        _world.WorktreeFiles.Touch(Red, A.At(10));
        _world.WorktreeFiles.Touch(Cand, A.At(10));
        await _world.Ready();
        var result = await new SweepWorktreesService(_world.Config, _world.Sweeper).Handle(new SweepWorktreesRequest(DryRun: false, Pr: pr), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_sweep_for_the_pr_that_closed_the_issue_removes_the_runs_trees()
    {
        var swept = await Sweep(2720);

        Assert.Equal([Cand, Red], swept.Removed.Select(static r => r.Path).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_sweep_for_another_pr_leaves_them_out()
    {
        var swept = await Sweep(2799);

        Assert.Empty(swept.Removed);
        Assert.DoesNotContain(swept.Kept, static r => string.Equals(r.Path, Red, StringComparison.Ordinal) || string.Equals(r.Path, Cand, StringComparison.Ordinal));
    }
}
