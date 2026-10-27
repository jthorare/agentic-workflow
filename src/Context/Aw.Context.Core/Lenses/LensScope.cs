using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>An anchor's own scope over an applied graph (spec §6.1 step 1) with the lookups every band shares.</summary>
internal sealed class LensScope
{
    private LensScope(GraphSnapshot graph, GraphNode anchor, CascadeResult cascade, IReadOnlyDictionary<string, GraphNode> nodes)
    {
        Graph = graph;
        Anchor = anchor;
        Cascade = cascade;
        Nodes = nodes;
        Own = cascade.OwnScope.Select(k => k.Value).ToHashSet(StringComparer.Ordinal);
    }

    internal GraphSnapshot Graph { get; }

    internal GraphNode Anchor { get; }

    internal CascadeResult Cascade { get; }

    internal IReadOnlyDictionary<string, GraphNode> Nodes { get; }

    /// <summary>The keys of the own scope.</summary>
    internal HashSet<string> Own { get; }

    /// <summary>The scope of <paramref name="anchor"/> (a key or an alias), cascading <paramref name="depth"/> levels; null when the graph has no such node.</summary>
    internal static LensScope? Of(GraphSnapshot graph, NodeKey anchor, int depth, CascadeOptions? options = null) => Of(graph, anchor, depth, options, NodesOf(graph));

    /// <summary>The scope as above, over a node lookup built once by <see cref="NodesOf"/> (a set of anchors shares one instead of building one per anchor).</summary>
    internal static LensScope? Of(GraphSnapshot graph, NodeKey anchor, int depth, CascadeOptions? options, IReadOnlyDictionary<string, GraphNode> nodes) =>
        graph.Resolve(anchor) is { } node ? new LensScope(graph, node, Core.Graph.Cascade.Affects(graph, node.Key, depth, options ?? CascadeOptions.Default), nodes) : null;

    /// <summary>Every node of <paramref name="graph"/> by key.</summary>
    internal static Dictionary<string, GraphNode> NodesOf(GraphSnapshot graph) => graph.Nodes.ToDictionary(n => n.Key.Value, StringComparer.Ordinal);

    /// <summary>The edges of the given types that end in the own scope.</summary>
    internal IEnumerable<GraphEdge> IntoScope(params EdgeType[] types) => EdgeLookup.For(Graph).Into(Own, types);

    /// <summary>Whether a human or the extractor stands behind the node, or an accepted override does (the Confirmation rule).</summary>
    internal static bool Confirmed(Provenance provenance) => Confirmation.Of(provenance);

    /// <summary>A node as the own-scope list shows it.</summary>
    internal static LensItem Item(GraphNode n) => new(n.Key, n.Label, n.Key.Kind, n.Provenance, n.Confidence, Confirmed(n.Provenance), TrustPolicy.ForNode(n, Confirmed(n.Provenance)));
}
