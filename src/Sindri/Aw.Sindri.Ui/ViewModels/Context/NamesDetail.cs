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
        return Labels.ContextMapNamesDetail;
    }
}
