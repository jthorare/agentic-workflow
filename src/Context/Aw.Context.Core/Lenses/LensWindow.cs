namespace Aw.Context.Core.Lenses;

/// <summary>One page of a lenses answer. The same window is taken from every list, and the counts stay the full totals.</summary>
/// <param name="Offset">How many items were skipped.</param>
/// <param name="Limit">How many items were kept from each list.</param>
/// <param name="More">True when a later offset still has items.</param>
public sealed record LensWindow(int Offset, int Limit, bool More)
{
    /// <summary>The full item count of each paged list by its path ('bands.product.own', 'edges'), as <c xml:lang="en">page.lists</c> does in the other tools.</summary>
    public IReadOnlyDictionary<string, int>? Totals { get; init; }
}
