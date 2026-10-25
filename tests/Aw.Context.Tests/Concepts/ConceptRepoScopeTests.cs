using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Concepts;

public sealed class ConceptRepoScopeTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_session_sees_the_concepts_its_repos_members_belong_to_and_not_the_others()
    {
        var graph = new GraphBuilder()
            .Node("code:src/a/Shifts.cs", "Shifts.cs").Node("feature:shifts", "Shifts").Node("concept:scheduling", "Scheduling")
            .Node("code:src/b/Invoices.cs", "Invoices.cs").Node("feature:invoices", "Invoices").Node("concept:billing", "Billing")
            .Edge("code:src/a/Shifts.cs", EdgeType.Implements, "feature:shifts", evidence: "repo:web/src/a/Shifts.cs")
            .Edge("code:src/b/Invoices.cs", EdgeType.Implements, "feature:invoices", evidence: "repo:api/src/b/Invoices.cs")
            .Edge("feature:shifts", EdgeType.BelongsTo, "concept:scheduling").Edge("feature:invoices", EdgeType.BelongsTo, "concept:billing")
            .Build();

        var scoped = graph.ScopedTo(ContextRepoName.From("web"));

        Assert.Equal(["code:src/a/Shifts.cs", "concept:scheduling", "feature:shifts"], scoped.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Contains(scoped.Edges, e => e.Type == EdgeType.BelongsTo);
    }
}
