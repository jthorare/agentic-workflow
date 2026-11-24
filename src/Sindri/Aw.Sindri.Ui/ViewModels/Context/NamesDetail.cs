using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The words for function names that are unavailable, one per reason the server gives: the Files view note and the coverage sentence. Each reason also
/// says whether the Code index card can fix it (#4184); a new reason is one line in <see cref="Known"/>.
/// </summary>
internal static class NamesDetail
{
    /// <summary>The detail for an unavailable symbols answer.</summary>
    /// <param name="symbols">What the lens served.</param>
    /// <returns>The words for its reason.</returns>
    internal static string For(SymbolsView symbols) => Known(symbols)?.Words ?? Labels.ContextMapNamesDetail;

    /// <summary>The coverage sentence's note for function names that were not searched: the reason's words, else the short "not indexed".</summary>
    /// <param name="symbols">What the lens served.</param>
    /// <returns>The words, and whether the Code index card can fix it.</returns>
    internal static NamesNote Sentence(SymbolsView symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        return (string.Equals(symbols.State, "unavailable", StringComparison.Ordinal) ? Known(symbols) : null) ?? new NamesNote(Labels.ContextMapNamesNotIndexed);
    }

    // The note for a reason other than "the index isn't built"; null for that one (or no reason), which each caller says its own way.
    private static NamesNote? Known(SymbolsView symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        return symbols.Reason?.Replace('-', ' ') switch
        {
            null or "index not built" => null,
            "index building" => new(Labels.ContextMapNamesBuilding),
            "index damaged" => new(Labels.ContextMapNamesDamaged),
            "not configured" => new(Labels.ContextMapNamesNotConfigured),
            "files are in no single repo" => new(Labels.ContextMapNamesSeveralRepos, CardFixes: false),   // one index per repo: the card can't join them
            _ => new(Labels.ContextMapNamesUnknown),
        };
    }
}
