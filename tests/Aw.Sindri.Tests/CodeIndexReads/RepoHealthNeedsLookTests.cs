using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Sindri.Features.CodeIndexReads;
using Aw.Sindri.Testing;
using RepoName = Aw.CodeIndex.Core.Values.RepoName;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>Each repo's health carries needsLook from doctor's own rule (issue #3430): a problem row is a look, a build on its way is not.</summary>
public sealed class RepoHealthNeedsLookTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoName _repo = RepoName.From("demo");
    private static readonly CommitSha _old = CommitSha.From(new string('1', 40));
    private static readonly CommitSha _new = CommitSha.From(new string('2', 40));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_behind_main_needs_a_look()
    {
        var health = await Read(Status(IndexState.Ok, _old), live: _new);

        Assert.Equal(("stale", true), (health.State, health.NeedsLook));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_damaged_index_needs_a_look()
    {
        var health = await Read(Status(IndexState.Damaged, commit: null), live: _old);

        Assert.Equal(("damaged", true), (health.State, health.NeedsLook));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_up_to_date_index_needs_no_look()
    {
        var health = await Read(Status(IndexState.Ok, _old), live: _old);

        Assert.Equal(("ok", false), (health.State, health.NeedsLook));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_build_on_its_way_needs_no_look()
    {
        var health = await Read(Status(IndexState.Missing, commit: null), live: _new);

        Assert.Equal(("queued", false), (health.State, health.NeedsLook));
    }

    private static RepoStatus Status(IndexState state, CommitSha? commit) =>
        new(_repo, state, commit, commit is null ? null : _now.AddHours(-1), [new LayerStatus(LayerName.Structure, LayerState.Ok, Code: null, "ok")], new IndexCounts(Files: 1, Symbols: 2, Edges: 3));

    private static async Task<RepoHealth> Read(RepoStatus status, CommitSha live)
    {
        var heads = new FakeRepoHeads().With(_repo, live, onDefaultBranch: true);
        var scope = new CodeIndexScope([new CodeIndexRepo(_repo, "/work/demo", CodeIndexSettings.Default)], new CodeIndexCaller(CallerKind.Service, Repo: null));
        var healths = await new RepoHealths(heads, new InMemoryJobViews(), TimeProvider.System, new QueuedClock(TimeProvider.System.GetUtcNow())).Of(scope, [status], Token);
        return Assert.Single(healths);
    }
}
