using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The sentence under an answer: what was searched and why the rest was not, built on the client from the answer's coverage, searched bands and symbols state.</summary>
internal static class CoverageWords
{
    /// <summary>The one plain sentence under the answer.</summary>
    /// <param name="view">The lenses answer.</param>
    /// <param name="exact">Whether the answer came from a direct pick or an exact name.</param>
    /// <returns>"Searched Code. No Why or Product records. Function names aren't indexed.", or empty on an exact hit or when every area was searched.</returns>
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
        var names = view.Bands.Code.Symbols.State;
        var stale = string.Equals(names, "stale", StringComparison.Ordinal);
        var missing = !stale && !string.Equals(names, "ok", StringComparison.Ordinal);
        if (!missing)
        {
            areas.Sort(Labels.ContextMapWordNames, none: false, some: stale);
        }

        return areas.Searched.Count == 4 ? string.Empty : Compose(areas, missing);
    }

    private static string Compose(Areas areas, bool namesMissing)
    {
        var (searched, empty, partly) = (areas.Searched, areas.Empty, areas.Partly);
        var parts = new List<string>();
        if (searched.Count > 0)
        {
            parts.Add(Labels.ContextMapSearchedSentence(searched));
        }
        else if (partly.Count == 0)
        {
            // Nothing was searched at all; with an area searched in part, the opening would contradict the partly sentence.
            parts.Add(Labels.ContextMapSearchedNothing);
        }

        if (empty.Count > 0)
        {
            parts.Add(Labels.ContextMapNoRecords(empty));
        }

        if (partly.Count > 0)
        {
            parts.Add(Labels.ContextMapPartlySearched(partly));
        }

        if (namesMissing)
        {
            parts.Add(Labels.ContextMapNamesNotIndexed);
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
