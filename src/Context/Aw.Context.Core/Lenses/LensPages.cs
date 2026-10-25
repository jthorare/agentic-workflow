namespace Aw.Context.Core.Lenses;

/// <summary>Takes one page of a lenses answer: the same offset and limit from every list that carries the bulk of the payload.</summary>
public static class LensPages
{
    /// <summary>The page of <paramref name="view"/> at <paramref name="offset"/>, <paramref name="limit"/> items long.</summary>
    /// <param name="view">The whole answer.</param>
    /// <param name="limit">How many items to keep from each list.</param>
    /// <param name="offset">How many items to skip.</param>
    /// <returns>The page. Counts and coverage are the full answer's.</returns>
    public static LensesView Window(LensesView view, int limit, int offset)
    {
        ArgumentNullException.ThrowIfNull(view);
        var window = new PageCut(limit, offset);
        var (bands, edges) = Cut(view.Bands, view.Edges, window);
        return view with { Bands = bands, Edges = edges, Page = window.Page() };
    }

    /// <summary>The page of <paramref name="view"/> at <paramref name="offset"/>, <paramref name="limit"/> items long.</summary>
    /// <param name="view">The whole answer.</param>
    /// <param name="limit">How many items to keep from each list.</param>
    /// <param name="offset">How many items to skip.</param>
    /// <returns>The page. Counts and coverage are the full answer's.</returns>
    public static LensesSetView Window(LensesSetView view, int limit, int offset)
    {
        ArgumentNullException.ThrowIfNull(view);
        var window = new PageCut(limit, offset);
        var (bands, edges) = Cut(view.Bands, view.Edges, window);
        return view with { Bands = bands, Edges = edges, Page = window.Page() };
    }

    private static (LensBands Bands, IReadOnlyList<LensEdge> Edges) Cut(LensBands bands, IReadOnlyList<LensEdge> edges, PageCut window)
    {
        var product = bands.Product;
        var intent = bands.Intent;
        var code = bands.Code;
        return (
            bands with
            {
                Product = product with
                {
                    Own = window.Take(product.Own, "bands.product.own"),
                    Neighbors = window.Take(product.Neighbors, "bands.product.neighbors"),
                    Affects = window.Take(product.Affects, "bands.product.affects"),
                    Edges = window.Take(product.Edges, "bands.product.edges"),
                },
                Intent = intent with
                {
                    Records = window.Take(intent.Records, "bands.intent.records"),
                    Decisions = window.Take(intent.Decisions, "bands.intent.decisions"),
                },
                Code = code with { Files = window.Take(code.Files, "bands.code.files") },
            },
            window.Take(edges, "edges"));
    }

    // One window over every list of an answer: what it kept, each list's full count, and whether any list has items past it.
    private sealed class PageCut(int limit, int offset)
    {
        private readonly Dictionary<string, int> _totals = new(StringComparer.Ordinal);
        private bool _more;

        internal LensWindow Page() => new(offset, limit, _more) { Totals = _totals };

        internal T[] Take<T>(IReadOnlyList<T> items, string path)
        {
            _totals[path] = items.Count;
            if (items.Count > (long)offset + limit)
            {
                _more = true;
            }

            if (offset >= items.Count)
            {
                return [];
            }

            var count = Math.Min(limit, items.Count - offset);
            var kept = new T[count];
            for (var index = 0; index < count; index++)
            {
                kept[index] = items[offset + index];
            }

            return kept;
        }
    }
}
