using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The pure part of the Pages and actions section: an answer's pages, in its order, as rows with their marks and actions (pages spec §3.4, §3.5).</summary>
internal static class PageRows
{
    /// <summary>The rows of an answer's pages list; a key the answer has no node for is skipped.</summary>
    /// <param name="answer">The answer.</param>
    /// <param name="concept">The opened concept's key, left out of "also in"; or null.</param>
    /// <returns>The rows, in the answer's order.</returns>
    internal static List<PageRowViewModel> Of(WholeMapView answer, string? concept)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var nodes = (answer.Nodes ?? []).GroupBy(n => n.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var edges = answer.Edges ?? [];
        var graph = new Graph(nodes, edges.ToLookup(e => e.To, StringComparer.Ordinal), edges.ToLookup(e => e.From, StringComparer.Ordinal));
        return [.. (answer.Pages ?? []).Where(nodes.ContainsKey).Select(key => Row(nodes[key], graph, concept))];
    }

    private static PageRowViewModel Row(WholeNode page, Graph graph, string? concept)
    {
        var (nodes, into, _) = graph;
        var actions = into[page.Key].Where(e => e.Type == "on" && nodes.TryGetValue(e.From, out var n) && n.Kind == "action").Select(e => nodes[e.From])
            .DistinctBy(n => n.Key, StringComparer.Ordinal).OrderByDescending(n => n.Owner).ThenBy(n => n.Label, StringComparer.CurrentCultureIgnoreCase).ThenBy(n => n.Key, StringComparer.Ordinal).ToList();
        var code = actions.Select(a => a.Key).Prepend(page.Key).Any(k => into[k].Any(e => e.Type == "implements"));
        string[] marks = [page.Owner ? Labels.ContextConceptSetByPerson : Labels.ContextPagesSuggested, code ? Labels.ContextPagesCode : Labels.ContextPagesNoCode];
        return new PageRowViewModel(page.Key, page.Label, marks, FromMap(page.Key), [.. actions.Select(a => Action(a, page, graph, concept))]);
    }

    private static PageActionRowViewModel Action(WholeNode action, WholeNode page, Graph graph, string? concept)
    {
        var (nodes, into, from) = graph;
        List<string> parts = [Labels.ContextPagesOn(page.Label)];
        var others = from[action.Key].Where(e => e.Type == "expresses" && !string.Equals(e.To, concept, StringComparison.Ordinal) && nodes.ContainsKey(e.To))
            .Select(e => nodes[e.To].Label).Distinct(StringComparer.Ordinal).ToList();
        if (others.Count > 0)
        {
            parts.Add(Labels.ContextPagesAlsoIn(others));
        }

        var decisions = into[action.Key].Count(e => e.Type == "decides");
        if (decisions > 0)
        {
            parts.Add(Labels.ContextPagesDecisions(decisions));
        }

        parts.Add(into[action.Key].FirstOrDefault(e => e.Type == "implements") is { } code ? Labels.ContextPagesCodeFile(FileName(nodes.TryGetValue(code.From, out var file) ? file.Label : code.From)) : Labels.ContextPagesNoCode);
        return new PageActionRowViewModel(action.Key, action.Label, "· " + string.Join(" · ", parts));
    }

    /// <summary>Gets a value indicating whether a page key is an owner page with no map page of its own ('surface:owner-…').</summary>
    /// <param name="key">The page's node key.</param>
    /// <returns>True for a page the owner added with no map page.</returns>
    internal static bool OwnerOnly(string key) => SurfaceName(key).Name.StartsWith("owner-", StringComparison.Ordinal);

    // The map's page in words: a route's method and path ("GET /schedule/week"), else the component's name; empty for a page the owner added with no map page.
    private static string FromMap(string key)
    {
        var (kind, name) = SurfaceName(key);
        if (OwnerOnly(key))
        {
            return string.Empty;
        }

        var colon = name.IndexOf(':', StringComparison.Ordinal);
        return Labels.ContextPagesFromMap(kind == "route" && colon > 0 ? name[..colon] + " " + name[(colon + 1)..] : name);
    }

    private static (string Kind, string Name) SurfaceName(string key)
    {
        var rest = key.StartsWith("surface:", StringComparison.Ordinal) ? key["surface:".Length..] : key;
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? (string.Empty, rest) : (rest[..slash], rest[(slash + 1)..]);
    }

    // The answer's nodes by key and its edges by each end.
    private readonly record struct Graph(Dictionary<string, WholeNode> Nodes, ILookup<string, WholeEdge> Into, ILookup<string, WholeEdge> From);

    private static string FileName(string path) => path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
}
