using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.CodeIndexReads;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;
using RepoName = Aw.CodeIndex.Core.Values.RepoName;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>
/// A repo with no index that waits behind another repo's running first build (the first-builds queue, #3322) is not a problem: its 30 min count starts only once no first build
/// runs ahead of it, and its waiting words name the build it waits behind.
/// </summary>
public sealed class QueuedBehindFirstBuildTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoName _demo = RepoName.From("demo");
    private static readonly RepoName _other = RepoName.From("other");
    private static readonly CommitSha _head = CommitSha.From(new string('2', 40));
    private static readonly CommitSha _built = CommitSha.From(new string('1', 40));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_queued_an_hour_behind_another_repos_first_build_needs_no_look_and_says_what_it_waits_behind()
    {
        var other = await Read(otherIndexed: false);

        Assert.Equal(("queued", false, RepoHealth.QueuedBehindWords("demo")), (other.State, other.NeedsLook, other.Waiting));
        Assert.Null(other.LookWords);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_queued_an_hour_while_another_repo_only_rebuilds_is_still_a_look()
    {
        var other = await Read(otherIndexed: true);

        Assert.True(other.NeedsLook);
    }

    // demo's first build runs; other has had no index for an hour (since serve start), or demo is indexed and only rebuilding
    private static async Task<RepoHealth> Read(bool otherIndexed)
    {
        var heads = new FakeRepoHeads().With(_demo, _head, onDefaultBranch: true).With(_other, _head, onDefaultBranch: true);
        var scope = new CodeIndexScope(
            [new CodeIndexRepo(_demo, "/work/demo", CodeIndexSettings.Default), new CodeIndexRepo(_other, "/work/other", CodeIndexSettings.Default)],
            new CodeIndexCaller(CallerKind.Service, Repo: null));
        var jobs = new InMemoryJobViews();
        jobs.Put(new JobRow(JobId.From("JOB-00000000000000000000000000000001"), JobKind.From("BuildCodeIndex"), JobKey.From("index:demo"), Item: null, Repo: null, Service: null, JobState.Running,
            "service:code-index-main-move", _now.AddMinutes(-5), EndedAt: null, [], Rounds: 0, Summary: null, Code: null, Result: null, CancelledBy: null, Sequence: 1));
        var clock = new QueuedClock(_now.AddHours(-1));
        clock.Seed(["demo", "other"]);
        var demo = otherIndexed ? Status(_demo, IndexState.Stale, _built) : Status(_demo, IndexState.Missing, commit: null);
        var healths = await new RepoHealths(heads, jobs, new NoPauses(), new FixedClock(_now), clock).Of(scope, [demo, Status(_other, IndexState.Missing, commit: null)], Token);
        return healths.Single(static h => h.Repo == "other");
    }

    private static RepoStatus Status(RepoName repo, IndexState state, CommitSha? commit) =>
        new(repo, state, commit, commit is null ? null : _now.AddHours(-1), [new LayerStatus(LayerName.Structure, LayerState.Ok, Code: null, "ok")], new IndexCounts(Files: 1, Symbols: 2, Edges: 3));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
