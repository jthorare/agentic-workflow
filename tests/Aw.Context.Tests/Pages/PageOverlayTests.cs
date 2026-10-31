using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Pages;

[Trait("Lane", "unit")]
public sealed class PageOverlayTests
{
    internal const string Surface = "surface:route/GET:/schedule/week";
    internal const string Action = "action:WeekPage.submit";

    internal static EvidenceRef[] InRepo(string repo, string file) => [new(RecordKey.From($"repo:{repo}/{file}"))];

    internal static GraphSnapshot MapOf(string repo = "web")
    {
        GraphNode[] nodes =
        [
            new(NodeKey.From(Surface), "GET /schedule/week", Provenance.Deterministic, 1.0, InRepo(repo, "src/Week.tsx")),
            new(NodeKey.From(Action), "Submit schedule", Provenance.Deterministic, 1.0, InRepo(repo, "src/Week.tsx")),
            new(NodeKey.From("code:src/Week.tsx"), "Week.tsx"),
            new(NodeKey.From("concept:self-scheduling"), "Self-scheduling"),
            new(NodeKey.From("concept:approval-workflow"), "Approval workflow"),
            new(NodeKey.From("concept:billing"), "Billing"),
        ];
        GraphEdge[] edges =
        [
            new(NodeKey.From("code:src/Week.tsx"), NodeKey.From(Surface), EdgeType.Implements, Provenance.Deterministic, 1.0, InRepo(repo, "src/Week.tsx")),
            new(NodeKey.From("code:src/Week.tsx"), NodeKey.From(Action), EdgeType.Implements, Provenance.Deterministic, 1.0, InRepo(repo, "src/Week.tsx")),
            new(NodeKey.From(Action), NodeKey.From(Surface), EdgeType.On, Provenance.Deterministic),
            new(NodeKey.From(Action), NodeKey.From("concept:billing"), EdgeType.Expresses, Provenance.Deterministic),
        ];
        return new GraphSnapshot(nodes, edges, [], []);
    }

    internal static PagePin Page(string repo = "web", string? surface = Surface, params string[] concepts) =>
        new(PageId.From("schedule-week"), "Schedule week", ContextRepoName.From(repo), surface,
            [new PageActionPin(PageActionId.From("submit-schedule"), "Submit a schedule", Action, concepts)]);

    internal static ContextRepo[] Repos(PagePin page, string repo = "web") =>
        [new ContextRepo(ContextRepoName.From(repo), "/work/" + repo) { Pages = [page] }];

    [Fact]
    public void A_named_page_relabels_its_surface_and_links_its_action_to_two_concepts()
    {
        var applied = PageOverlay.Apply(MapOf(), Repos(Page(concepts: ["self-scheduling", "approval-workflow"])));

        var surface = Assert.Single(applied.Nodes, n => n.Key.Value == Surface);
        Assert.Equal("Schedule week", surface.Label);
        Assert.Equal(Provenance.Deterministic, surface.Provenance);
        Assert.Contains(surface.EvidenceOrEmpty, r => r.Key.Value == "repo:web/src/Week.tsx");
        Assert.Contains(surface.EvidenceOrEmpty, r => r.Key.Value == "page:web/schedule-week");
        var action = Assert.Single(applied.Nodes, n => n.Key.Value == Action);
        Assert.Equal("Submit a schedule", action.Label);
        var expresses = applied.Edges.Where(e => e.Type == EdgeType.Expresses && e.From.Value == Action).ToList();
        Assert.Equal("concept:approval-workflow,concept:self-scheduling", string.Join(',', expresses.Select(e => e.To.Value).Order(StringComparer.Ordinal)));
        Assert.All(expresses, e => Assert.Equal(Provenance.Human, e.Provenance));
        Assert.All(expresses, e => Assert.Contains(e.EvidenceOrEmpty, r => r.Key.Value == "page:web/schedule-week"));
    }

    [Fact]
    public void A_page_without_a_surface_key_adds_an_owner_screen_and_an_owner_edge_replaces_the_suggested_one()
    {
        var applied = PageOverlay.Apply(MapOf(), Repos(Page(surface: null, concepts: ["self-scheduling"])));

        var owner = Assert.Single(applied.Nodes, n => n.Key.Value == "surface:screen/owner-schedule-week");
        Assert.Equal(Provenance.Human, owner.Provenance);
        Assert.DoesNotContain(applied.Edges, e => e.Type == EdgeType.Expresses && e.To.Value == "concept:billing");
        Assert.Contains(applied.Edges, e => e.Type == EdgeType.Expresses && e.To.Value == "concept:self-scheduling");
    }

    [Fact]
    public void A_key_the_map_no_longer_has_falls_back_to_an_owner_node_and_a_stale_concept_makes_no_edge()
    {
        var applied = PageOverlay.Apply(MapOf(), Repos(Page(surface: "surface:route/GET:/gone", concepts: ["self-schedul"])));

        Assert.Contains(applied.Nodes, n => n.Key.Value == "surface:screen/owner-schedule-week" && n.Label == "Schedule week");
        Assert.DoesNotContain(applied.Edges, e => e.Type == EdgeType.Expresses && e.From.Value == Action && e.Provenance == Provenance.Human);
        Assert.Equal(MapOf().Nodes.Count + 1, applied.Nodes.Count);
    }

    [Fact]
    public void A_surface_shared_with_another_repo_keeps_its_own_label_and_the_page_gets_an_owner_node_with_its_own_code()
    {
        var shared = MapOf();
        shared = shared with
        {
            Edges = [.. shared.Edges, new GraphEdge(NodeKey.From("code:admin/Week.tsx"), NodeKey.From(Surface), EdgeType.Implements, Provenance.Deterministic, 1.0, InRepo("admin", "Week.tsx"))],
        };

        var applied = PageOverlay.Apply(shared, Repos(Page(surface: Surface)));

        Assert.Equal("GET /schedule/week", Assert.Single(applied.Nodes, n => n.Key.Value == Surface).Label);
        Assert.Contains(applied.Edges, e => e.Type == EdgeType.Implements && e.To.Value == "surface:screen/owner-schedule-week" && e.From.Value == "code:src/Week.tsx");
        Assert.DoesNotContain(applied.Edges, e => e.To.Value == "surface:screen/owner-schedule-week" && e.From.Value == "code:admin/Week.tsx");
    }

    [Fact]
    public void Applying_the_pages_twice_gives_the_same_graph_and_without_takes_them_out_again()
    {
        var repos = Repos(Page(concepts: ["self-scheduling"]));
        var once = PageOverlay.Apply(MapOf(), repos);
        var twice = PageOverlay.Apply(once, repos);

        Assert.Equal(once.Nodes.Count, twice.Nodes.Count);
        Assert.Equal(once.Edges.Count, twice.Edges.Count);
        var without = PageOverlay.Without(PageOverlay.Apply(MapOf(), Repos(Page(surface: null, concepts: ["self-scheduling"]))));
        Assert.DoesNotContain(without.Nodes, n => n.Key.Value.Contains("owner", StringComparison.Ordinal));
        Assert.DoesNotContain(without.Edges, e => e.EvidenceOrEmpty.Any(r => r.Key.Value.StartsWith("page:", StringComparison.Ordinal)));
    }

    [Fact]
    public void An_owner_page_is_in_its_repos_scope_and_not_another_repos()
    {
        var applied = PageOverlay.Apply(MapOf(), Repos(Page(surface: null, concepts: ["self-scheduling"])));

        var web = applied.ScopedTo(ContextRepoName.From("web"));
        var api = applied.ScopedTo(ContextRepoName.From("api"));

        Assert.Contains(web.Nodes, n => n.Key.Value == "surface:screen/owner-schedule-week");
        Assert.Contains(web.Nodes, n => n.Key.Value == "concept:self-scheduling");
        Assert.DoesNotContain(api.Nodes, n => n.Key.Value == "surface:screen/owner-schedule-week");
    }
}
