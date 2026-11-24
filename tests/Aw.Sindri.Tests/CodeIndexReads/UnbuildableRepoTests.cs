using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.CodeIndexReads;
using IndexRepoHead = Aw.CodeIndex.Core.Model.RepoHead;
using RepoName = Aw.CodeIndex.Core.Values.RepoName;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>A repo whose first code index can never build (#3187): off its default branch, with no default branch to build from, it is 'unbuildable', a look whose words name the fix.</summary>
public sealed class UnbuildableRepoTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoName _repo = RepoName.From("demo");
    private static readonly CommitSha _head = CommitSha.From(new string('2', 40));
    private static readonly CommitSha _tip = CommitSha.From(new string('1', 40));

    private static RepoStatus Missing() => new(_repo, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null);

    private static RepoHealth Health(IndexRepoHead head) => RepoHealth.Of(Missing(), head, [], _now);

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, false, "unbuildable")]
    [InlineData(true, false, "queued")]
    [InlineData(false, true, "queued")]
    public void Only_a_repo_off_its_default_branch_with_no_default_tip_is_unbuildable(bool onDefault, bool hasTip, string state)
        => Assert.Equal(state, Health(new IndexRepoHead(_repo, _head, onDefault, hasTip ? _tip : null)).State);

    [Fact, Trait("Lane", "unit")]
    public void An_unbuildable_repo_is_a_problem_whose_words_name_the_fix()
    {
        var problem = CodeIndexProblems.For(Missing(), Health(new IndexRepoHead(_repo, _head, OnDefaultBranch: false)));

        Assert.Equal(
            ("SND-CODEIDX-013", "demo: can't build a code index: no default branch to build from (no origin/HEAD, main or master); check out the default branch or fetch origin", "the code index for demo has no default branch to build from"),
            (problem?.Code, problem?.Text, problem?.Clause));
    }

    // why: a pause starts nothing, but nothing would start anyway; the repo stays unbuildable with its own words, not "missing · paused"
    [Fact, Trait("Lane", "unit")]
    public void A_paused_build_service_leaves_an_unbuildable_repo_unbuildable_and_unpaused()
    {
        var health = Health(new IndexRepoHead(_repo, _head, OnDefaultBranch: false)).Unqueued(buildServiceActive: false);

        Assert.Equal(("unbuildable", false), (health.State, health.Paused));
    }

    // #4091: a hand-started build failed on the repo, so it is in backoff; paused, no retry comes, and none could build it anyway.
    [Fact, Trait("Lane", "unit")]
    public void A_failed_build_in_backoff_over_an_unbuildable_repo_reads_unbuildable_with_no_retry_while_paused()
    {
        var failed = new JobRow(JobId.From($"JOB-{1:x32}"), JobKind.From("BuildCodeIndex"), JobKey.From("index:demo"), Item: null, Repo: null, Service: null, JobState.Failed,
            "human", _now.AddSeconds(-15), _now.AddSeconds(-10), [], Rounds: 0, Summary: null, Code: null, Result: null, CancelledBy: null, Sequence: 1);
        var waiting = RepoHealth.Of(Missing(), new IndexRepoHead(_repo, _head, OnDefaultBranch: false), [failed], _now);

        var health = waiting.Unqueued(buildServiceActive: false);

        Assert.Equal("waiting", waiting.State);
        Assert.Equal(("unbuildable", (string?)null, false), (health.State, health.Waiting, health.Paused));
    }
}
