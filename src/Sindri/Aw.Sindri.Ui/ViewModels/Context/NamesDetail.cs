using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The sentence under the function names area when the symbols are unavailable: one per reason the server gives.</summary>
internal static class NamesDetail
{
    /// <summary>The detail for an unavailable symbols answer.</summary>
    /// <param name="symbols">What the lens served.</param>
    /// <returns>The words for its reason.</returns>
    internal static string For(SymbolsView symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        return symbols.Reason?.Replace('-', ' ') switch
        {
            null or "index not built" => Labels.ContextMapNamesDetail,
            "index building" => Labels.ContextMapNamesBuilding,
            "index damaged" => Labels.ContextMapNamesDamaged,
            "not configured" => Labels.ContextMapNamesNotConfigured,
            "files are in no single repo" => Labels.ContextMapNamesSeveralRepos,
            _ => Labels.ContextMapNamesUnknown,
        };
    }
}
