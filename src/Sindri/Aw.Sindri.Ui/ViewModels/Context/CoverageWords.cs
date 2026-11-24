using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The sentence under an answer: what was searched and why the rest was not, built on the client from the answer's coverage, searched bands and symbols state.
/// The function names are not an area: they get their own words, "indexed" as on the code band (#4122), then where to look.
/// </summary>
internal static class CoverageWords
{
    /// <summary>The one plain sentence under the answer.</summary>
    /// <param name="view">The lenses answer.</param>
    /// <param name="exact">Whether the answer came from a direct pick or an exact name.</param>
    /// <returns>"Searched Code. No Why or Product records. Function names aren't indexed yet. See Code index on the System page.", or empty on an exact hit or when every area and the function names were searched.</returns>
    internal static string Sentence(LensesView view, bool exact)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (exact)
        {
            return string.Empty;
        }

        var c = view.Coverage;
        var areas = new Areas();
        areas.Sort(NodeBands.Title(WholeBand.Intent), view.Searched.Bands.Intent.Empty, c.Intent.Covered < c.Intent.Of);
        areas.Sort(NodeBands.Title(WholeBand.Product), view.Searched.Bands.Product.Empty, c.Product.Confirmed < c.Product.Of);
        areas.Sort(NodeBands.Title(WholeBand.Code), view.Searched.Bands.Code.Empty, c.Code.Implemented < c.Code.Of);
        var symbols = view.Bands.Code.Symbols;
        var ok = string.Equals(symbols.State, "ok", StringComparison.Ordinal);
        if (ok && areas.Searched.Count == 3)
        {
            return string.Empty;
        }

        // Only "ok" and "stale" mean the names were searched. Any other state (today "unavailable"; a later one such as "building") reads as not
        // searched, and NamesDetail.Sentence gives the reason's words, or "aren't indexed yet" when it has none.
        var stale = string.Equals(symbols.State, "stale", StringComparison.Ordinal);
        var names = ok ? null : stale ? Labels.ContextMapNamesStale : NamesDetail.Sentence(symbols);
        return Compose(areas, names, missing: !ok && !stale);
    }

    private static string Compose(Areas areas, string? names, bool missing)
    {
        var parts = new List<string>();
        if (areas.Searched.Count > 0 || areas.Partly.Count > 0)
        {
            parts.Add(Labels.ContextMapSearchedSentence(areas.Searched, areas.Partly));
        }
        else if (missing)
        {
            // Nothing was searched at all; with the function names searched in full or in part, the opening would contradict them.
            parts.Add(Labels.ContextMapSearchedNothing);
        }

        if (areas.Empty.Count > 0)
        {
            parts.Add(Labels.ContextMapNoRecords(areas.Empty));
        }

        if (names is not null)
        {
            parts.Add(names);   // why the function names were not searched in full (#3777)
            parts.Add(Labels.ContextMapNamesNextStep);   // and where to look (#4122)
        }

        return string.Join(' ', parts);
    }

    /// <summary>The area words, sorted by how much of each area was searched.</summary>
    private sealed class Areas
    {
        public List<string> Searched { get; } = [];

        public List<string> Empty { get; } = [];

        public List<string> Partly { get; } = [];

        public void Sort(string word, bool none, bool some)
        {
            if (none)
            {
                Empty.Add(word);
            }
            else if (some)
            {
                Partly.Add(word);
            }
            else
            {
                Searched.Add(word);
            }
        }
    }
}
