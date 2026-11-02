using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The INSERTs that write one map version, and its override rows. Trust is computed here from provenance, never read from a file.</summary>
internal static class GraphStatements
{
    public static IEnumerable<SqlStatement> Graph(long version, GraphSnapshot graph)
    {
        foreach (var node in graph.Nodes)
        {
            yield return Node(version, node);
            foreach (var (evidence, i) in node.EvidenceOrEmpty.Select((e, i) => (e, i)))
            {
                yield return new SqlStatement(GraphSql.InsertNodeEvidence, c => c.Bind("$v", version).Bind("$key", node.Key.Value)
                    .Bind("$position", i).Bind("$record", evidence.Key.Value).Bind("$span", evidence.Span));
            }
        }

        foreach (var alias in graph.Aliases)
        {
            yield return new SqlStatement(GraphSql.InsertAlias, c => c.Bind("$v", version).Bind("$alias", alias.Alias.Value).Bind("$target", alias.Target.Value));
        }

        foreach (var edge in graph.Edges)
        {
            foreach (var statement in Edge(version, edge))
            {
                yield return statement;
            }
        }

        foreach (var d in graph.Decisions)
        {
            yield return new SqlStatement(GraphSql.InsertDecision, c => c.Bind("$v", version).Bind("$key", d.ToKey().Value).Bind("$quote", d.Quote)
                .Bind("$wording", d.Wording).Bind("$provenance", GraphText.Of(d.Provenance))
                .Bind("$trust", GraphText.Of(TrustPolicy.For(d.Provenance, accepted: false))).Bind("$at", GraphText.Time(d.At)));
        }
    }

    public static SqlStatement Override(MapOverride o) => new(GraphSql.InsertOverride, c => c
        .Bind("$id", o.ToId().Value).Bind("$action", GraphText.Of(o.Action)).Bind("$target", o.Target.Value).Bind("$label", o.Label)
        .Bind("$other", o.Other?.Value).Bind("$who", o.Who).Bind("$at", GraphText.Time(o.At)).Bind("$rejection", o.Rejection)
        .Bind("$link", o.Link?.Value).Bind("$undoes", o.Undoes?.Value).Bind("$payload", GraphPayload.Write(o.Proposal)));

    public static SqlStatement Run(ScopeProposal proposal) => new(GraphSql.UpsertRun, c => c
        .Bind("$run", proposal.Run).Bind("$repo", proposal.Repo.Value).Bind("$surfaces", GraphPayload.WriteSurfaces(proposal.Surfaces)));

    private static SqlStatement Node(long version, GraphNode node) => new(GraphSql.InsertNode, c => c
        .Bind("$v", version).Bind("$key", node.Key.Value).Bind("$label", node.Label).Bind("$attributes", node.Attributes?.GetRawText())
        .Bind("$provenance", GraphText.Of(node.Provenance)).Bind("$confidence", node.Confidence)
        .Bind("$trust", GraphText.Of(TrustPolicy.For(node.Provenance, accepted: false))).Bind("$at", GraphText.Time(node.At)));

    private static IEnumerable<SqlStatement> Edge(long version, GraphEdge edge)
    {
        var type = GraphText.Of(edge.Type);
        yield return new SqlStatement(GraphSql.InsertEdge, c => c
            .Bind("$v", version).Bind("$from", edge.From.Value).Bind("$to", edge.To.Value).Bind("$type", type)
            .Bind("$provenance", GraphText.Of(edge.Provenance)).Bind("$confidence", edge.Confidence)
            .Bind("$trust", GraphText.Of(TrustPolicy.For(edge.Provenance, accepted: false))).Bind("$order", edge.Order)
            .Bind("$at", GraphText.Time(edge.At)).Bind("$count", edge.Count));
        foreach (var (evidence, i) in edge.EvidenceOrEmpty.Select((e, i) => (e, i)))
        {
            yield return new SqlStatement(GraphSql.InsertEdgeEvidence, c => c.Bind("$v", version).Bind("$from", edge.From.Value)
                .Bind("$to", edge.To.Value).Bind("$type", type).Bind("$position", i).Bind("$record", evidence.Key.Value).Bind("$span", evidence.Span));
        }
    }
}
