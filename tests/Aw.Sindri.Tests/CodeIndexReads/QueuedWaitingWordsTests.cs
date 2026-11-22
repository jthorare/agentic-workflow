using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Sindri.Features.CodeIndexReads;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;
using RepoName = Aw.CodeIndex.Core.Values.RepoName;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>A queued repo's health says what its first build waits for (#3515): the service's next tick, unless it has been queued too long, when the look words say why instead.</summary>
public sealed class QueuedWaitingWordsTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoName _repo = RepoName.From("demo");
    private static readonly CommitSha _head = CommitSha.From(new string('2', 40));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_repo_says_its_first_build_starts_on_the_next_tick()
    {
        var health = await Read(Status(IndexState.Missing, commit: null), queuedSince: _now);

        Assert.Equal(("queued", false, RepoHealth.QueuedWords), (health.State, health.NeedsLook, health.Waiting));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_queued_too_long_has_look_words_and_no_waiting_words()
    {
        var health = await Read(Status(IndexState.Missing, commit: null), queuedSince: _now.AddMinutes(-CodeIndexProblems.QueuedLimitMinutes));

        Assert.True(health.NeedsLook);
        Assert.NotNull(health.LookWords);
        Assert.Null(health.Waiting);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_up_to_date_repo_has_no_waiting_words()
    {
        var health = await Read(Status(IndexState.Ok, _head), queuedSince: _now);

        Assert.Equal(("ok", (string?)null), (health.State, health.Waiting));
    }

    private static RepoStatus Status(IndexState state, CommitSha? commit) =>
        new(_repo, state, commit, commit is null ? null : _now.AddHours(-1), [new LayerStatus(LayerName.Structure, LayerState.Ok, Code: null, "ok")], new IndexCounts(Files: 1, Symbols: 2, Edges: 3));

    private static async Task<RepoHealth> Read(RepoStatus status, DateTimeOffset queuedSince)
    {
        var heads = new FakeRepoHeads().With(_repo, _head, onDefaultBranch: true);
        var scope = new CodeIndexScope([new CodeIndexRepo(_repo, "/work/demo", CodeIndexSettings.Default)], new CodeIndexCaller(CallerKind.Service, Repo: null));
        var clock = new QueuedClock(queuedSince);
        clock.Seed(["demo"]);
        var healths = await new RepoHealths(heads, new InMemoryJobViews(), new NoPauses(), new FixedClock(_now), clock).Of(scope, [status], Token);
        return Assert.Single(healths);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
