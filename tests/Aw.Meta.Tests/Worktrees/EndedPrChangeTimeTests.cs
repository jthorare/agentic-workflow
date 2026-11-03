using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Worktrees;
using Aw.Meta.Features.MergeRun;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>#2011 follow-up: rule 7 judges the tree of a merged or closed pull request against when that pull request ended, not against the global sweep marker.</summary>
public sealed class EndedPrChangeTimeTests
{
    private const string Lane = "/work/.claude/worktrees/lane";
    private const string Loose = "/work/.claude/worktrees/loose";
    private readonly MetaWorld _world = new();

    private static string Stamp(int minutes) => A.At(minutes).ToString("O", CultureInfo.InvariantCulture);

    private static TeamConfig Team() => new("t", "/work", ["api"], "meta");

    private sealed class MergedRun : IService<MergeRunRequest, MergeRunResponse>
    {
        public ValueTask<Result<MergeRunResponse>> Handle(MergeRunRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success(new MergeRunResponse(request.Pr, Merged: true, [], "sha", "announce")));
    }

    // The shared checkout, the tree of pull request 7's branch "feat" and (optionally) a tree with no pull request; `pr` is the gh pr list row for 7.
    private void Script(string pr, bool withLoose = false)
    {
        _ = _world.Runner
            .When("git fetch", string.Empty)
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("worktree list --porcelain", "worktree /work\nHEAD aaaaaaa\nbranch refs/heads/main\n\nworktree " + Lane + "\nHEAD bbbbbbb\nbranch refs/heads/feat\n"
                + (withLoose ? "\nworktree " + Loose + "\nHEAD ddddddd\nbranch refs/heads/loose\n" : string.Empty))
            .When("gh pr list", "[" + pr + "]")
            .When("--not origin/main", "0\n")
            .When("--not --remotes", "0\n")
            .When("status --porcelain", string.Empty)
            .When("rev-parse --show-toplevel", request => new CommandResult(0, request.Cwd + "\n", string.Empty, TimedOut: false))
            .When("worktree remove", string.Empty)
            .When("worktree prune", string.Empty);
    }

    private static string Row(string state, string ended) =>
        "{\"number\":7,\"state\":\"" + state + "\",\"headRefName\":\"feat\",\"headRefOid\":\"bbbbbbb\"" + ended + "}";

    private static string Merged(int minutes) => Row("MERGED", ",\"mergedAt\":\"" + Stamp(minutes) + "\"");

    private async Task<string> MergeRun()
    {
        await _world.Ready();
        var result = await new SweepAfterMergeService(new MergedRun(), _world.Config, _world.Sweeper).Handle(new MergeRunRequest(7), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value.Worktrees ?? string.Empty;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_run_sweep_removes_a_tree_last_changed_before_the_pull_request_merged()
    {
        Script(Merged(90));
        _ = await _world.Values.Set(KvKeys.WorktreesSweepStart, Stamp(0), CancellationToken.None);
        _world.WorktreeFiles.Touch(Lane, A.At(60));

        var line = await MergeRun();

        Assert.Contains("removed 1", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_run_sweep_removes_such_a_tree_when_no_sweep_was_ever_recorded()
    {
        Script(Merged(90));
        _world.WorktreeFiles.Touch(Lane, A.At(60));

        Assert.Contains("removed 1", await MergeRun(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_run_sweep_keeps_a_tree_changed_after_the_pull_request_merged()
    {
        Script(Merged(90));
        _ = await _world.Values.Set(KvKeys.WorktreesSweepStart, Stamp(0), CancellationToken.None);
        _world.WorktreeFiles.Touch(Lane, A.At(100));

        var line = await MergeRun();

        Assert.Contains("kept 1", line, StringComparison.Ordinal);
        Assert.Contains("changed since PR #7 merged", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_closed_pull_request_is_judged_against_when_it_closed()
    {
        Script(Row("CLOSED", ",\"closedAt\":\"" + Stamp(90) + "\""));
        _ = await _world.Values.Set(KvKeys.WorktreesSweepStart, Stamp(0), CancellationToken.None);
        _world.WorktreeFiles.Touch(Lane, A.At(60));
        Assert.Contains("removed 1", await MergeRun(), StringComparison.Ordinal);

        _world.WorktreeFiles.Touch(Lane, A.At(100));
        var line = await MergeRun();

        Assert.Contains("kept 1", line, StringComparison.Ordinal);
        Assert.Contains("changed since PR #7 closed", line, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData(",\"mergedAt\":null")]
    public async Task A_merged_pull_request_whose_end_time_is_missing_keeps_its_tree_with_that_reason(string ended)
    {
        Script(Row("MERGED", ended));
        _world.WorktreeFiles.Touch(Lane, A.At(60));

        var line = await MergeRun();

        Assert.Contains("kept 1", line, StringComparison.Ordinal);
        Assert.Contains("PR #7 ended, but when could not be read (kept)", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_run_sweep_leaves_the_marker_alone_and_a_later_full_sweep_still_keeps_a_no_pr_tree_changed_after_it()
    {
        Script(Merged(90), withLoose: true);
        _ = await _world.Values.Set(KvKeys.WorktreesSweepStart, Stamp(0), CancellationToken.None);
        _world.WorktreeFiles.Touch(Lane, A.At(60));
        _world.WorktreeFiles.Touch(Loose, A.At(10));

        _ = await MergeRun();
        Assert.Equal(Stamp(0), (await _world.Values.Get(KvKeys.WorktreesSweepStart, CancellationToken.None)).Value);

        var full = await _world.Sweeper.Sweep(Team(), WorktreeScope.AllIncludingNoPr, SweepMode.Remove, "sweep", CancellationToken.None);

        var loose = Assert.Single(full.Value.Kept, r => string.Equals(r.Path, Loose, StringComparison.Ordinal));
        Assert.Contains(loose.Reasons, r => r.Contains("changed since the last sweep", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_full_sweep_keeps_a_tree_edited_after_the_merge_then_removes_it_once_that_sweep_has_passed()
    {
        Script(Merged(90));
        _ = await _world.Values.Set(KvKeys.WorktreesSweepStart, Stamp(0), CancellationToken.None);
        _world.WorktreeFiles.Touch(Lane, A.At(100));
        _world.Clock.Advance(TimeSpan.FromMinutes(60));

        var first = await _world.Sweeper.Sweep(Team(), WorktreeScope.All, SweepMode.Remove, "sweep", CancellationToken.None);

        var kept = Assert.Single(first.Value.Kept, r => string.Equals(r.Path, Lane, StringComparison.Ordinal));
        Assert.Contains(kept.Reasons, r => r.Contains("changed since PR #7 merged", StringComparison.Ordinal));
        Assert.Equal(Stamp(120), (await _world.Values.Get(KvKeys.WorktreesSweepStart, CancellationToken.None)).Value);

        _world.Clock.Advance(TimeSpan.FromMinutes(10));
        var second = await _world.Sweeper.Sweep(Team(), WorktreeScope.All, SweepMode.Remove, "sweep", CancellationToken.None);

        Assert.Contains(second.Value.Removed, r => string.Equals(r.Path, Lane, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_marker_later_than_the_merge_wins_and_is_named()
    {
        Script(Merged(90));
        _ = await _world.Values.Set(KvKeys.WorktreesSweepStart, Stamp(110), CancellationToken.None);
        _world.WorktreeFiles.Touch(Lane, A.At(115));

        Assert.Contains("changed since the last sweep", await MergeRun(), StringComparison.Ordinal);
    }
}
