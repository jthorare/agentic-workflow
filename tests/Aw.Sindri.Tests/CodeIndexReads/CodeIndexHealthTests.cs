using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CodeIndexReads;
using Aw.Sindri.Testing;
using RepoName = Aw.CodeIndex.Core.Values.RepoName;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>The code index's rows for GET /status: every approved repo is read, and nothing approved or nothing readable adds no row.</summary>
public sealed class CodeIndexHealthTests
{
    private static readonly CommitSha _old = CommitSha.From(new string('1', 40));
    private static readonly CommitSha _new = CommitSha.From(new string('2', 40));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CodeIndexHealth Health(FakeCodeIndex index, FakeRepoHeads heads, InMemoryProfiles? live = null) => new(index, new RepoHealths(heads, new InMemoryJobViews(), TimeProvider.System), live ?? new InMemoryProfiles());

    private static LoadedProfile Approved()
    {
        var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("demo", "/work/demo"), new RepoEntry("other", "/work/other")]);
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_approved_adds_no_row()
    {
        var problems = await Health(new FakeCodeIndex(), new FakeRepoHeads()).Problems(approved: null, Token);

        Assert.Empty(problems);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_graph_that_is_down_in_an_approved_repo_is_one_row()
    {
        var repo = RepoName.From("demo");
        var index = new FakeCodeIndex().WithIndex(repo, _old).WithGraphUnavailable(repo);
        var heads = new FakeRepoHeads().With(repo, _old, onDefaultBranch: true);

        var problems = await Health(index, heads).Problems(Approved(), Token);

        Assert.Equal(["SND-CODEIDX-008"], problems.Select(static p => p.Code), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_head_past_the_index_is_a_stale_row_and_the_scope_is_every_repo_not_one_callers()
    {
        var repo = RepoName.From("demo");
        var index = new FakeCodeIndex().WithIndex(repo, _old);
        var heads = new FakeRepoHeads().With(repo, _new, onDefaultBranch: true);

        var problems = await Health(index, heads).Problems(Approved(), Token);

        Assert.Equal(["SND-CODEIDX-014"], problems.Select(static p => p.Code), StringComparer.Ordinal);
        Assert.Equal(CallerKind.Service, index.Scopes[0].Caller.Kind);
        Assert.Equal(2, index.Scopes[0].Repos.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_live_only_repo_is_read_and_listed_too_owner_q102_CIX_L59()
    {
        var fresh = RepoName.From("fresh");
        var index = new FakeCodeIndex().WithIndex(fresh, _old);
        var heads = new FakeRepoHeads().With(fresh, _new, onDefaultBranch: true);
        var live = new InMemoryProfiles { Live = new LoadedProfile(new SindriProfile(1, "thor", Repos: [new RepoEntry("fresh", "/work/fresh")]), ProfileHash.From(new string('a', 64)), "/home/profile.yaml") };

        var problems = await Health(index, heads, live).Problems(Approved(), Token);

        Assert.Contains("fresh", index.Scopes[0].Repos.Select(static r => r.Name.Value), StringComparer.Ordinal);
        Assert.Equal(["SND-CODEIDX-014"], problems.Select(static p => p.Code), StringComparer.Ordinal);
    }
}
