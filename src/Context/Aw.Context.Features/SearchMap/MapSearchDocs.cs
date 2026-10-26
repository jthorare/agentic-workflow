using System.Text;
using System.Text.Json;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.Search;
using Aw.Context.Core.WholeMap;

namespace Aw.Context.Features.SearchMap;

/// <summary>
/// Cuts a graph into search documents, one per node, in key order so ids and ties are the same for the same graph: the label as the title, the file paths and evidence keys as the path,
/// a decision's quote and the node's descriptive attributes as the text. Everything is cleaned like every answer's text (ingest, then egress), so a planted secret is neither found nor shown.
/// </summary>
internal static class MapSearchDocs
{
    private const int MaxEvidence = 8;
    private const int MaxText = 4000;
    private const string RepoScheme = "repo:";

    internal static IReadOnlyList<IndexedNode> Of(GraphSnapshot graph, AreaIndex areas, MapText text)
    {
        var quotes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var decision in graph.Decisions)
        {
            _ = quotes.TryAdd(decision.ToKey().Value, decision.Quote);
        }

        return [.. graph.Nodes.OrderBy(static n => n.Key.Value, StringComparer.Ordinal).Select((n, id) => Node(n, id, quotes.GetValueOrDefault(n.Key.Value), areas, text))];
    }

    private static IndexedNode Node(GraphNode node, int id, string? quote, AreaIndex areas, MapText text)
    {
        var trust = TrustPolicy.ForNode(node, node.Provenance == Provenance.Human);
        var area = areas.ByNode.TryGetValue(node.Key.Value, out var a) ? a.Area : AreaIndex.UnassignedKey;
        var words = new StringBuilder();
        _ = words.Append(quote);
        AppendStrings(node.Attributes, words);
        var doc = new MapSearchDoc(id, text.Cleaned(node.Label, singleLine: true), text.Line(Paths(node)), words.Length == 0 ? string.Empty : text.Cleaned(words.ToString(), singleLine: false), area);
        return new IndexedNode(node, doc, RepoOf(node), trust);
    }

    private static string Paths(GraphNode node)
    {
        var paths = new List<string>();
        if (node.Key.Kind == NodeKind.Code)
        {
            paths.Add(node.Key.Name);
        }

        paths.AddRange(node.EvidenceOrEmpty.Take(MaxEvidence).Select(static e => e.Key.Value.StartsWith(RepoScheme, StringComparison.Ordinal) ? e.Key.Value[RepoScheme.Length..] : e.Key.Value));
        return string.Join(' ', paths);
    }

    private static string? RepoOf(GraphNode node)
    {
        var scoped = node.EvidenceOrEmpty.Select(static e => e.Key.Value).FirstOrDefault(static k => k.StartsWith(RepoScheme, StringComparison.Ordinal));
        if (scoped is null)
        {
            return null;
        }

        var rest = scoped[RepoScheme.Length..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 ? rest[..slash] : null;
    }

    private static void AppendStrings(JsonElement? element, StringBuilder into)
    {
        if (element is not { } e || into.Length >= MaxText)
        {
            return;
        }

        switch (e.ValueKind)
        {
            case JsonValueKind.String:
                _ = into.Append(' ').Append(e.GetString());
                break;
            case JsonValueKind.Object:
                foreach (var property in e.EnumerateObject())
                {
                    AppendStrings(property.Value, into);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in e.EnumerateArray())
                {
                    AppendStrings(item, into);
                }

                break;
            case JsonValueKind.Undefined or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(element), e.ValueKind, "an unknown JSON value kind");
        }
    }
}
