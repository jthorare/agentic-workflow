using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Tests.Map;

public sealed class ConceptMapDocumentTests
{
    private const string Header = "schemaVersion: 1\nkind: authored\nrepo: scheduler\n";
    private const string Nodes = "nodes:\n  - {key: \"concept:scheduling\", label: Scheduling}\n  - {key: \"feature:shifts\", label: Shifts}\n  - {key: \"thing:roster\", label: Roster}\n";

    [Fact, Trait("Lane", "unit")]
    public void A_map_with_a_concept_node_and_belongs_to_edges_validates()
    {
        var yaml = Header + Nodes + "edges:\n  - {from: \"feature:shifts\", to: \"concept:scheduling\", type: belongs-to}\n";

        var map = Maps.Checked(Maps.Parse(yaml));

        Assert.Equal(3, map.Counts.Nodes);
        Assert.Contains(map.Graph.Edges, e => e.Type == EdgeType.BelongsTo && e.To.Kind == NodeKind.Concept);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("  - {from: \"thing:roster\", to: \"concept:scheduling\", type: belongs-to}\n")]
    [InlineData("  - {from: \"concept:scheduling\", to: \"feature:shifts\", type: belongs-to}\n")]
    [InlineData("  - {from: \"feature:shifts\", to: \"concept:scheduling\", type: part-of}\n")]
    public void A_belongs_to_edge_between_other_kinds_is_refused(string edge)
    {
        var check = MapDocumentValidator.Check(Maps.Parse(Header + Nodes + "edges:\n" + edge), KeyMigrations.Shipped);

        Assert.True(check.IsError);
        Assert.Equal("SND-CONTEXT-031", check.Error.Code);
    }
}
