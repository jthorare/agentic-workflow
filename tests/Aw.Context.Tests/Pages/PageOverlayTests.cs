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
}
