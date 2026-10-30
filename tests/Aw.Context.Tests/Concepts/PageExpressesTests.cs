using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Concepts;

[Trait("Lane", "unit")]
public sealed class PageExpressesTests
{
    [Fact]
    public void An_action_whose_code_is_homed_in_billing_suggests_expressing_billing()
    {
        var graph = new ConceptGraph()
            .Concept("concept:billing", "Billing")
            .Node("feature:invoices", "Invoices").Node("action:pay", "Pay invoice").Node("code:web/Pay.tsx")
            .Member("feature:invoices", "concept:billing")
            .Edge("code:web/Pay.tsx", EdgeType.Implements, "feature:invoices").Edge("code:web/Pay.tsx", EdgeType.Implements, "action:pay")
            .Build();

        var edge = Assert.Single(PageExpresses.Apply(graph).Edges, e => e.Type == EdgeType.Expresses);

        Assert.Equal("action:pay", edge.From.Value);
        Assert.Equal("concept:billing", edge.To.Value);
        Assert.Equal(Provenance.Deterministic, edge.Provenance);
    }
}
