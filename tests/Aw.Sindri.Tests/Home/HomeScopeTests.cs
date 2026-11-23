using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.HomeRoutes;

namespace Aw.Sindri.Tests.Home;

/// <summary>What an agent session sees of Home (home spec §9): its repo's rows and the rows of no repo, never another repo's.</summary>
public sealed class HomeScopeTests
{
    private static readonly HomeScope _demo = new(RepoName.From("demo"));

    [Theory, Trait("Lane", "unit")]
    [InlineData(HomeKind.Dm), InlineData(HomeKind.Mention), InlineData(HomeKind.ThreadReply), InlineData(HomeKind.Keyword), InlineData(HomeKind.AgentLimited), InlineData(HomeKind.System)]
    public void A_row_of_no_repo_is_seen_by_every_session(HomeKind kind)
    {
        var row = HomeFixture.Row("slack:x:1", position: 1, kind: kind, repo: null);

        Assert.True(HomeScope.RepoLess(kind));
        Assert.True(new HomeScope(Repo: null).Sees(row));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_row_of_the_sessions_repo_is_seen_and_another_repos_is_not()
    {
        Assert.True(_demo.Sees(HomeFixture.Row("gh:review:a/demo#1", position: 1, repo: "demo")));
        Assert.False(_demo.Sees(HomeFixture.Row("gh:review:a/other#1", position: 1, repo: "other")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_row_with_no_repo_named_is_not_seen_by_a_session() =>
        Assert.False(_demo.Sees(HomeFixture.Row("lane:recycle:qa", position: 1, kind: HomeKind.LaneRecycle, repo: null)));

    [Fact, Trait("Lane", "unit")]
    public void A_session_with_no_repo_sees_no_repo_row() =>
        Assert.False(new HomeScope(Repo: null).Sees(HomeFixture.Row("gh:review:a/demo#1", position: 1, repo: "demo")));

    [Fact, Trait("Lane", "unit")]
    public void Paging_with_a_scope_counts_only_what_it_sees()
    {
        HomeRow[] rows = [HomeFixture.Row("gh:review:a/demo#1", position: 1, repo: "demo"), HomeFixture.Row("gh:review:a/other#1", position: 2, repo: "other")];

        var page = HomePaging.Page(rows, new HomeQuery(Group: null, [HomeState.Open], After: null, Limit: 0, _demo));

        Expect.Sequence(["gh:review:a/demo#1"], page.Rows.Select(static r => r.Id.Value));
        Assert.Equal(1, page.Counts.NeedsYou);
    }
}
