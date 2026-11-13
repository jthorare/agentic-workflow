using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The sentence under an answer: what was searched and why the rest was not, built on the client from the answer's coverage, searched bands and symbols state.</summary>
internal static class CoverageWords
{
    /// <summary>The one plain sentence under the answer.</summary>
    /// <param name="view">The lenses answer.</param>
    /// <param name="exact">Whether the answer came from a direct pick or an exact name.</param>
    /// <returns>"Searched code. No intent or product records. Function names not indexed.", or empty on an exact hit or when every area was searched.</returns>
    internal static string Sentence(LensesView view, bool exact)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (exact)
        {
            return string.Empty;
        }

        var c = view.Coverage;
        var searched = new List<string>();
        var empty = new List<string>();
        var partly = new List<string>();
        Sort(Labels.ContextMapWordIntent, view.Searched.Bands.Intent.Empty, c.Intent.Covered < c.Intent.Of, searched, empty, partly);
        Sort(Labels.ContextMapWordProduct, view.Searched.Bands.Product.Empty, c.Product.Confirmed < c.Product.Of, searched, empty, partly);
        Sort(Labels.ContextMapWordCode, view.Searched.Bands.Code.Empty, c.Code.Implemented < c.Code.Of, searched, empty, partly);
        var names = view.Bands.Code.Symbols.State;
        var stale = string.Equals(names, "stale", StringComparison.Ordinal);
        var missing = !stale && !string.Equals(names, "ok", StringComparison.Ordinal);
        Sort(Labels.ContextMapWordNames, missing, stale, searched, [], partly);
        return searched.Count == 4 ? string.Empty : Compose(searched, empty, partly, missing);
    }

    private static string Compose(List<string> searched, List<string> empty, List<string> partly, bool namesMissing)
    {
        var parts = new List<string> { searched.Count == 0 ? Labels.ContextMapSearchedNothing : Labels.ContextMapSearchedSentence(searched) };
        if (empty.Count > 0)
        {
            parts.Add(Labels.ContextMapNoRecords(empty));
        }

        if (namesMissing)
        {
            parts.Add(Labels.ContextMapNamesNotIndexed);
        }

        if (partly.Count > 0)
        {
            parts.Add(Labels.ContextMapPartlySearched(partly));
        }

        return string.Join(' ', parts);
    }

    private static void Sort(string word, bool none, bool some, List<string> searched, List<string> noneAreas, List<string> someAreas)
    {
        if (none)
        {
            noneAreas.Add(word);
        }
        else if (some)
        {
            someAreas.Add(word);
        }
        else
        {
            searched.Add(word);
        }
    }
}
