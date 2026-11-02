using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ScopeGraph;

/// <summary>A scoping graph anchored on a concept (issue 1315): it holds the concept's member features and the concept node reads its summary attribute, not its label.</summary>
public sealed class ScopeGraphConceptAnchorTests : IDisposable
{
    private const string Concept = "concept:tab-management";
    private const string Manager = "feature:tabs.tab-manager";
    private const string Groups = "feature:tabs.tab-groups";
    private const string Unrelated = "feature:billing.invoices";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static string Yaml() => MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"concept:tab-management\", label: \"Tab management\", attributes: {summary: \"Opening, grouping and closing browser tabs\", source: derived}}",
        "  - {key: \"feature:tabs.tab-manager\", label: \"Tab manager\"}",
        "  - {key: \"feature:tabs.tab-groups\", label: \"Tab groups\"}",
        "  - {key: \"feature:billing.invoices\", label: \"Invoices\"}",
        "edges:",
        "  - {from: \"feature:tabs.tab-manager\", to: \"concept:tab-management\", type: belongs-to}",
        "  - {from: \"feature:tabs.tab-groups\", to: \"concept:tab-management\", type: belongs-to}");

    private async Task<ScopeGraphView> Build()
    {
        _ = await _world.ImportYaml(Yaml());
        var answer = await _world.Engine.ScopeGraph(new ScopeGraphQuery([Concept], MapRead.Current, new LensRange(0, Hops: null), Brief: null, 400, 300_000), MapWorld.Human, MapWorld.None);
        Assert.True(answer.IsSuccess, answer.IsError ? answer.Error.Message : null);
        return answer.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ScopeGraph_expands_a_concept_anchor_to_its_member_features()
    {
        var view = await Build();

        var ids = view.Nodes.Select(n => n.Id.Value).ToList();
        Assert.True(ids.Contains(Manager, StringComparer.Ordinal) && ids.Contains(Groups, StringComparer.Ordinal), "nodes: " + string.Join(", ", ids));
        Assert.DoesNotContain(Unrelated, ids, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ScopeGraph_summarizes_a_concept_node_with_its_summary_attribute()
    {
        var view = await Build();

        var concept = Assert.Single(view.Nodes, n => n.Id.Value == Concept);
        Assert.Equal("Opening, grouping and closing browser tabs", concept.Summary);
    }
}
