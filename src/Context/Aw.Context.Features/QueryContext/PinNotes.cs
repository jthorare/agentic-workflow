using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// What an answer says about the pin it was read under (concept pins spec §3.2): the scoped line, an anchor that sits in another pin, a text question that anchored on a pin
/// without <c xml:lang="en">concept=</c>, and, for the diff shape, the changed files outside the pin (which also leave the anchor set).
/// </summary>
internal static class PinNotes
{
    /// <summary>Puts the notes on <paramref name="graph"/>; the graph is returned as is when no pin is in play.</summary>
    /// <param name="graph">The graph part with its <see cref="GraphPart.Pins"/>.</param>
    /// <param name="text">Fences an alias, which is owner text, like a label.</param>
    /// <param name="isText">True for the text shape (the only one the hint is for).</param>
    /// <returns>The graph part with its notes, and for a diff under a pin, the anchors the pin holds.</returns>
    internal static GraphPart Apply(GraphPart graph, bool isText, MapText text)
    {
        var pins = graph.Pins;
        if (pins.Scope is not { } scope)
        {
            return isText ? graph with { Notes = Hints(graph, text) } : graph;
        }

        var id = scope.Pin.Value;
        var notes = new List<string> { $"scoped to concept {id} (map {graph.MapVersion ?? "none"})" };
        notes.AddRange(Aliased(graph, text));
        notes.AddRange(pins.AnchorPins.Where(p => !string.Equals(p.Value, id, StringComparison.Ordinal))
            .Select(p => $"anchor concept:{p.Value} is outside concept {id}, so its lens shows only {id}'s part"));
        if (graph.Diff is not { } diff)
        {
            return graph with { Notes = notes };
        }

        var outside = diff.Files.Count(f => !scope.HoldsPath(diff.Repo, f.Path) && !f.Nodes.Any(n => NodeKey.TryFrom(n.Key, out var key) && scope.HoldsNode(key)));
        if (outside > 0)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture, $"concept {id}: {outside} of {diff.Files.Count} changed files are outside it"));
        }

        return graph with { Notes = notes, Anchors = [.. graph.Anchors.Where(a => NodeKey.TryFrom(a, out var key) && scope.HoldsNode(key))] };
    }

    // An anchor reached through a pin's alias says so; the alias is fenced like a label.
    private static IEnumerable<string> Aliased(GraphPart graph, MapText text) =>
        graph.AnchorAliases.Select(a => NodeKey.TryFrom(a.Key, out var key) ? AnchorMatch.AliasLine(key, text.Label(a.Value, Trust.Untrusted)) : null).OfType<string>();

    // A text question that anchored on a pin's concept node: say how to see only that pin.
    private static List<string> Hints(GraphPart graph, MapText text) =>
        [.. graph.Anchors.Where(a => a.StartsWith("concept:", StringComparison.Ordinal) && graph.Pins.Pins.Visible.Any(p => string.Equals("concept:" + p.Value, a, StringComparison.Ordinal)))
            .Select(a => (graph.AnchorAliases.TryGetValue(a, out var alias) && NodeKey.TryFrom(a, out var key) ? AnchorMatch.AliasLine(key, text.Label(alias, Trust.Untrusted)) : $"anchored on {a}")
                + $"; add concept={a["concept:".Length..]} to see only {a["concept:".Length..]}")];
}
