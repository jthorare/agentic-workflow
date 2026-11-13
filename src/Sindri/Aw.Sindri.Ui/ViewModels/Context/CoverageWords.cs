using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The coverage strip's words, built on the client from the answer's coverage, searched bands and symbols state.</summary>
internal static class CoverageWords
{
    /// <summary>The four areas and the sentence over them.</summary>
    /// <param name="view">The lenses answer.</param>
    /// <returns>The areas, the lead ("3 of 4 areas searched.") and the rest ("Code is limited.").</returns>
    internal static (IReadOnlyList<CoverageArea> Areas, string Lead, string Tail) Words(LensesView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var c = view.Coverage;
        var symbols = view.Bands.Code.Symbols.State;
        IReadOnlyList<CoverageArea> areas =
        [
            Area(Labels.ContextMapAreaIntent, view.Searched.Bands.Intent.Empty, c.Intent.Covered, c.Intent.Of),
            Area(Labels.ContextMapAreaProduct, view.Searched.Bands.Product.Empty, c.Product.Confirmed, c.Product.Of),
            Area(Labels.ContextMapAreaCode, view.Searched.Bands.Code.Empty, c.Code.Implemented, c.Code.Of),
            Level(Labels.ContextMapAreaNames, string.Equals(symbols, "ok", StringComparison.Ordinal) ? CoverageLevel.Searched : string.Equals(symbols, "stale", StringComparison.Ordinal) ? CoverageLevel.Limited : CoverageLevel.NotAvailable,
                string.Equals(symbols, "unavailable", StringComparison.Ordinal) ? Labels.ContextMapNamesDetail : null),
        ];
        var searched = areas.Count(a => a.Level == CoverageLevel.Searched);
        var limited = areas.Where(a => a.Level == CoverageLevel.Limited).Select(a => a.Name).ToList();
        return (areas, Labels.ContextMapCoverageLead(searched, areas.Count), limited.Count == 0 ? string.Empty : Labels.ContextMapCoverageRest(limited));
    }

    /// <summary>The one plain sentence under the answer: what was searched and why the rest was not.</summary>
    /// <param name="view">The lenses answer.</param>
    /// <param name="exact">Whether the answer came from a direct pick or an exact name.</param>
    /// <returns>The sentence, or empty on an exact hit or when every area was searched.</returns>
    internal static string Sentence(LensesView view, bool exact) => throw new NotSupportedException();

    private static CoverageArea Area(string name, bool empty, int covered, int of) =>
        Level(name, empty ? CoverageLevel.NotAvailable : covered < of ? CoverageLevel.Limited : CoverageLevel.Searched, detail: null);

    private static CoverageArea Level(string name, CoverageLevel level, string? detail) => new(name, level, level switch
    {
        CoverageLevel.Searched => Labels.ContextMapSearched,
        CoverageLevel.Limited => Labels.ContextMapLimited,
        CoverageLevel.NotAvailable => Labels.ContextMapNotAvailable,
        _ => Labels.ContextMapNotAvailable,
    }, detail);
}
