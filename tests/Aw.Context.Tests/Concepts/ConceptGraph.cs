using System.Text.Json;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Concepts;

/// <summary>A graph with concepts, written by hand the way the derivation will write it: a concept node with its attributes and citations, and a 'belongs-to' edge from each member.</summary>
internal sealed class ConceptGraph
{
    private readonly List<GraphNode> _nodes = [];
    private readonly List<GraphEdge> _edges = [];

    public static MapText Text { get; } = new(new FakeScrubberFactory().For([]).Value, FencePolicy.Clean);

    public static ConceptsBasis Basis { get; } = new("v7", Repo: null);

    public ConceptGraph Concept(string key, string name, string? summary = null, string source = "derived", params string[] cites)
    {
        var attributes = JsonSerializer.SerializeToElement(new Dictionary<string, string?>(StringComparer.Ordinal) { ["summary"] = summary, ["source"] = source, ["seed"] = "feature:seed" });
        _nodes.Add(new GraphNode(NodeKey.From(key), name, Provenance.Deterministic, 1.0, [.. cites.Select(c => new EvidenceRef(RecordKey.From(c)))], attributes));
        return this;
    }

    public ConceptGraph Node(string key, string? label = null)
    {
        _nodes.Add(new GraphNode(NodeKey.From(key), label ?? key));
        return this;
    }

    public ConceptGraph Member(string member, string concept)
    {
        _edges.Add(new GraphEdge(NodeKey.From(member), NodeKey.From(concept), EdgeType.BelongsTo));
        return this;
    }

    public ConceptGraph Edge(string from, EdgeType type, string to)
    {
        _edges.Add(new GraphEdge(NodeKey.From(from), NodeKey.From(to), type));
        return this;
    }

    public GraphSnapshot Build() => new(_nodes, _edges, [], []);

    /// <summary>Two concepts over a small scheduling product: shifts and swaps belong to 'scheduling', invoices to 'billing', a lone feature and a lone record to nothing.</summary>
    public static ConceptGraph Product() => new ConceptGraph()
        .Concept("concept:scheduling", "Self scheduling", "People pick and swap shifts.", "derived", "repo:web/docs/scheduling.md")
        .Concept("concept:billing", "Billing", "Invoices and payment.", "override")
        .Node("feature:shifts", "Shifts").Node("feature:swaps", "Swaps").Node("feature:invoices", "Invoices").Node("feature:lone", "Loose end")
        .Node("record:swap-rules", "Swap rules").Node("decision:round-up", "Round up hours").Node("record:orphan", "Orphan note")
        .Node("code:src/Shifts.cs").Node("code:src/Shared.cs").Node("code:src/Invoices.cs").Node("surface:screen/shifts", "Shifts screen").Node("action:pick", "Pick a shift")
        .Member("feature:shifts", "concept:scheduling").Member("feature:swaps", "concept:scheduling").Member("feature:invoices", "concept:billing")
        .Member("record:swap-rules", "concept:scheduling").Member("decision:round-up", "concept:billing")
        .Edge("code:src/Shifts.cs", EdgeType.Implements, "feature:shifts").Edge("code:src/Shared.cs", EdgeType.Implements, "feature:shifts").Edge("code:src/Shared.cs", EdgeType.Implements, "feature:swaps")
        .Edge("code:src/Invoices.cs", EdgeType.Implements, "feature:invoices")
        .Edge("feature:invoices", EdgeType.DependsOn, "feature:shifts").Edge("feature:swaps", EdgeType.DependsOn, "feature:invoices").Edge("feature:lone", EdgeType.DependsOn, "feature:shifts")
        .Edge("surface:screen/shifts", EdgeType.PartOf, "feature:shifts").Edge("action:pick", EdgeType.On, "surface:screen/shifts");
}
