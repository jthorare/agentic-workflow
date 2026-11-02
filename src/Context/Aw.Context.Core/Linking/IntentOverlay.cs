using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Linking;

/// <summary>Merges the intent layer into a graph (S-5B): 'record:' nodes, 'mentions' edges (a link made only by a feature's name is a model-provenance suggestion until confirmed), decision nodes with 'quotes', 'decides' and 'supersedes'. Targets resolve through aliases.</summary>
public static class IntentOverlay
{
    /// <summary>Applies the layer.</summary>
    /// <param name="graph">The graph (a map version's, or an applied one).</param>
    /// <param name="layer">What syncs wrote.</param>
    /// <returns>The merged graph and the orphaned links; the same graph when the layer is empty.</returns>
    public static IntentOverlayResult Apply(GraphSnapshot graph, IntentLayer layer)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(layer);
        if (layer.Records.Count == 0 && layer.Links.Count == 0 && layer.Decisions.Count == 0 && layer.DecisionEdges.Count == 0)
        {
            return new IntentOverlayResult(graph, []);
        }

        var merge = new Merge(graph, layer);
        merge.AddNodes();
        merge.AddMentions();
        merge.AddDecisionEdges();
        return merge.Result();
    }

    private sealed class Merge(GraphSnapshot graph, IntentLayer layer)
    {
        private readonly List<GraphNode> _nodes = [.. graph.Nodes];
        private readonly List<GraphEdge> _edges = [.. graph.Edges];
        private readonly HashSet<(string From, string To)> _mentionKeys = [.. graph.Edges.Where(e => e.Type == EdgeType.Mentions).Select(e => (e.From.Value, e.To.Value))];
        private readonly List<IntentLink> _orphaned = [];
        private readonly List<NodeAlias> _newAliases = [];
        private readonly HashSet<string> _present = graph.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        private readonly Dictionary<string, NodeKey> _aliases = graph.Aliases.ToDictionary(a => a.Alias.Value, a => a.Target, StringComparer.Ordinal);

        public void AddNodes()
        {
            var linked = layer.Links.Select(l => l.Record.Value).Concat(layer.Decisions.Select(d => d.Record.Value)).ToHashSet(StringComparer.Ordinal);
            var targeted = layer.Links.Where(l => l.Target.Kind == NodeKind.Record).Select(l => l.Target.Value).ToHashSet(StringComparer.Ordinal);
            // A record no rule linked, and no link names, is stored (so it isn't fetched again) but is not part of the graph.
            foreach (var record in layer.Records.Where(r => linked.Contains(r.Key.Value) || targeted.Contains(RecordNode(r.Key).Value) || r.Aliases.Any(a => targeted.Contains("record:" + a))))
            {
                AddNode(new GraphNode(RecordNode(record.Key), record.Title, Provenance.Deterministic));
                foreach (var alias in record.Aliases.Where(a => NodeKey.TryFrom("record:" + a, out _)))
                {
                    var aliasKey = NodeKey.From("record:" + alias);
                    _newAliases.Add(new NodeAlias(aliasKey, RecordNode(record.Key)));
                    _ = _aliases.TryAdd(aliasKey.Value, RecordNode(record.Key));   // so a link by issue key resolves to the record, whichever was fetched first
                }
            }

            foreach (var decision in layer.Decisions)
            {
                AddNode(new GraphNode(decision.ToKey(), decision.Wording, decision.Provenance, At: decision.At));
            }
        }

        public void AddMentions()
        {
            var mentions = new Dictionary<(string From, string To), (double Confidence, List<EvidenceRef> Evidence, bool Suggested)>();
            foreach (var link in layer.Links.Where(l => _present.Contains(RecordNode(l.Record).Value)))
            {
                if (Resolve(link.Target) is not { } target)
                {
                    if (link.Target.Kind != NodeKind.Record)
                    {
                        _orphaned.Add(link);   // a link to a record that isn't synced yet waits for it; it isn't orphaned
                    }

                    continue;
                }

                var pair = (RecordNode(link.Record).Value, target.Value);
                var evidence = new EvidenceRef(link.Record, link.SpanHash);
                var suggested = link.Rule == LinkRule.Name;
                mentions[pair] = mentions.TryGetValue(pair, out var seen)
                    ? (Math.Max(seen.Confidence, link.Confidence), [.. seen.Evidence, evidence], seen.Suggested && suggested)
                    : (link.Confidence, [evidence], suggested);
            }

            foreach (var m in mentions.OrderBy(m => m.Key.From, StringComparer.Ordinal).ThenBy(m => m.Key.To, StringComparer.Ordinal))
            {
                AddMention(new GraphEdge(NodeKey.From(m.Key.From), NodeKey.From(m.Key.To), EdgeType.Mentions, m.Value.Suggested ? Provenance.Model : Provenance.Deterministic, m.Value.Confidence, m.Value.Evidence));
            }
        }

        public void AddDecisionEdges()
        {
            foreach (var decision in layer.Decisions.Where(d => _present.Contains(RecordNode(d.Record).Value)))
            {
                _edges.Add(new GraphEdge(decision.ToKey(), RecordNode(decision.Record), EdgeType.Quotes, decision.Provenance, At: decision.At));
            }

            foreach (var edge in layer.DecisionEdges.Where(e => _present.Contains(e.From.Value)))
            {
                if (Resolve(edge.To) is { } to)
                {
                    _edges.Add(edge with { To = to });
                }
            }
        }

        public IntentOverlayResult Result()
        {
            var known = graph.Decisions.Select(d => d.ToKey().Value).ToHashSet(StringComparer.Ordinal);
            var decisions = graph.Decisions.Concat(layer.Decisions.Where(d => known.Add(d.ToKey().Value))).ToList();
            var taken = graph.Aliases.Select(a => a.Alias.Value).ToHashSet(StringComparer.Ordinal);
            return new IntentOverlayResult(new GraphSnapshot(_nodes, _edges, [.. graph.Aliases, .. _newAliases.Where(a => taken.Add(a.Alias.Value))], decisions), _orphaned);
        }

        // A 'mentions' edge the graph already stores (a record's cited-path mentions, #1373) is not added twice: a lens would count it twice. Decision edges are not deduped: a second 'quotes' edge may carry its own evidence or time.
        private void AddMention(GraphEdge edge)
        {
            if (_mentionKeys.Add((edge.From.Value, edge.To.Value)))
            {
                _edges.Add(edge);
            }
        }

        private void AddNode(GraphNode node)
        {
            if (_present.Add(node.Key.Value))
            {
                _nodes.Add(node);
            }
        }

        private NodeKey? Resolve(NodeKey key) =>
            _present.Contains(key.Value) ? key : _aliases.TryGetValue(key.Value, out var target) && _present.Contains(target.Value) ? target : null;
    }

    private static NodeKey RecordNode(Sources.RecordKey key) => NodeKey.From("record:" + key.Value);
}
