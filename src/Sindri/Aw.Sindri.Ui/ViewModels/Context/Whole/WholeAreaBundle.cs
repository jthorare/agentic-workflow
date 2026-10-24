namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Every edge between one pair of areas, as one line between their tiles' centres.</summary>
/// <param name="From">One area.</param>
/// <param name="To">The other.</param>
/// <param name="Count">How many edges, of every type and both directions.</param>
/// <param name="Confirmed">How many are confirmed.</param>
/// <param name="X1">The line's start x.</param>
/// <param name="Y1">The line's start y.</param>
/// <param name="X2">The line's end x.</param>
/// <param name="Y2">The line's end y.</param>
internal sealed record WholeAreaBundle(string From, string To, int Count, int Confirmed, double X1, double Y1, double X2, double Y2)
{
    /// <summary>One bundle per pair of areas.</summary>
    /// <param name="edges">The served area edges, one per pair and type.</param>
    /// <returns>The bundles, in the order the pairs first appear.</returns>
    internal static IReadOnlyList<WholeAreaBundle> Of(IReadOnlyList<PlacedAreaEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        var pairs = new Dictionary<(string, string), WholeAreaBundle>();
        foreach (var edge in edges.Where(e => !string.Equals(e.From, e.To, StringComparison.Ordinal)))
        {
            var key = string.CompareOrdinal(edge.From, edge.To) <= 0 ? (edge.From, edge.To) : (edge.To, edge.From);
            pairs[key] = pairs.TryGetValue(key, out var seen)
                ? seen with { Count = seen.Count + edge.Count, Confirmed = seen.Confirmed + edge.Confirmed }
                : new WholeAreaBundle(edge.From, edge.To, edge.Count, edge.Confirmed, edge.X1, edge.Y1, edge.X2, edge.Y2);
        }

        return [.. pairs.Values];
    }
}
