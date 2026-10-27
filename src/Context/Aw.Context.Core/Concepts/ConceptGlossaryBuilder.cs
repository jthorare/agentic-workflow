using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Concepts;

/// <summary>Builds the glossary answer from a graph: the terms stored on its concept nodes. Pure apart from the text policy.</summary>
public static class ConceptGlossaryBuilder
{
    /// <summary>The terms of every concept of a graph.</summary>
    /// <param name="basis">The map version and repo the answer is for.</param>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <returns>The view.</returns>
    public static ConceptGlossaryView Build(ConceptsBasis basis, GraphSnapshot graph, MapText text)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(text);
        var concepts = graph.Nodes
            .Where(n => n.Key.Kind == NodeKind.Concept)
            .GroupBy(n => n.Key.Value, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(n => n.Key.Value, StringComparer.Ordinal)
            .Select(n => new ConceptTermsRow(n.Key, text.Label(n.Label, TrustPolicy.ForNode(n, n.Provenance == Provenance.Human)), Terms(n, text)))
            .Where(c => c.Terms.Count > 0)
            .ToList();
        var terms = concepts
            .SelectMany(c => c.Terms.Select(t => (Term: t, c.Id)))
            .GroupBy(p => p.Term, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new TermConceptsRow(g.Key, [.. g.Select(p => p.Id)]))
            .ToList();
        return new ConceptGlossaryView(1, basis.Repo, basis.MapVersion, concepts.Count == 0 ? ConceptStates.Empty : ConceptStates.Ok, concepts, terms);
    }

    private static List<string> Terms(GraphNode concept, MapText text)
    {
        var trust = TrustPolicy.ForNode(concept, concept.Provenance == Provenance.Human);
        return [.. ConceptTermsAttribute.Of(concept).Select(t => text.Label(t, trust))];
    }

    /// <summary>The glossary the concept nodes of a graph carry: each stored term with the member features of the concepts that have it.</summary>
    /// <param name="graph">The graph, concepts included.</param>
    /// <returns>The glossary (no clusters), or null when no concept carries a term.</returns>
    public static MapGlossary? Glossary(GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var members = graph.Edges
            .Where(static e => e.Type == EdgeType.BelongsTo && e.To.Kind == NodeKind.Concept && e.From.Kind == NodeKind.Feature)
            .GroupBy(static e => e.To.Value, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.Select(static e => e.From).DistinctBy(static k => k.Value, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        var features = new Dictionary<string, SortedDictionary<string, NodeKey>>(StringComparer.Ordinal);
        foreach (var concept in graph.Nodes.Where(static n => n.Key.Kind == NodeKind.Concept))
        {
            var terms = ConceptTermsAttribute.Of(concept);
            for (var i = 0; i < terms.Count; i++)
            {
                AddTerm(weights, features, terms[i], (double)(terms.Count - i) / terms.Count, members.GetValueOrDefault(concept.Key.Value) ?? []);
            }
        }

        var all = weights.Where(w => features[w.Key].Count > 0).OrderByDescending(static w => w.Value).ThenBy(static w => w.Key, StringComparer.Ordinal)
            .Select(w => new GlossaryTerm(w.Key, w.Value, [.. features[w.Key].Values])).ToList();
        return all.Count == 0 ? null : new MapGlossary(all, [], []);
    }

    // A term several concepts carry takes the features of all of them and its best weight.
    private static void AddTerm(Dictionary<string, double> weights, Dictionary<string, SortedDictionary<string, NodeKey>> features, string term, double weight, List<NodeKey> members)
    {
        weights[term] = Math.Max(weights.GetValueOrDefault(term), weight);
        if (!features.TryGetValue(term, out var keys))
        {
            features[term] = keys = new SortedDictionary<string, NodeKey>(StringComparer.Ordinal);
        }

        foreach (var member in members)
        {
            keys[member.Value] = member;
        }
    }
}
