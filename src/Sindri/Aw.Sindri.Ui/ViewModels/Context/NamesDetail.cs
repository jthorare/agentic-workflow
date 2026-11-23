using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The words for function names that are unavailable, one per reason the server gives: the Files view note and the coverage sentence.</summary>
internal static class NamesDetail
{
    /// <summary>The detail for an unavailable symbols answer.</summary>
    /// <param name="symbols">What the lens served.</param>
    /// <returns>The words for its reason.</returns>
    internal static string For(SymbolsView symbols) => Known(symbols) ?? Labels.ContextMapNamesDetail;

    /// <summary>
    /// The coverage sentence's words for function names that were not searched: the repos with no code index when the answer names them (several repos in scope, #3861),
    /// else the reason's words, else the short "not indexed".
    /// </summary>
    /// <param name="symbols">What the lens served.</param>
    /// <returns>The words.</returns>
    internal static string Sentence(SymbolsView symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        if (!string.Equals(symbols.State, "unavailable", StringComparison.Ordinal))
        {
            return Labels.ContextMapNamesNotIndexed;
        }

        return Known(symbols) ?? (symbols.Repos is { Count: > 0 } repos ? Labels.ContextMapNamesNoIndexFor(repos) : Labels.ContextMapNamesNotIndexed);
    }

    // The words for a reason other than "the index isn't built"; null for that one (or no reason), which each caller says its own way.
    private static string? Known(SymbolsView symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        return symbols.Reason?.Replace('-', ' ') switch
        {
            null or "index not built" => null,
            "index building" => Labels.ContextMapNamesBuilding,
            "index damaged" => Labels.ContextMapNamesDamaged,
            "not configured" => Labels.ContextMapNamesNotConfigured,
            "files are in no single repo" => Labels.ContextMapNamesSeveralRepos,
            "no files" => Labels.ContextMapNamesNoFiles,   // #3863: nothing to search, so not "not available right now"
            _ => Labels.ContextMapNamesUnknown,
        };
    }
}
