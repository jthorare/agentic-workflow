using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>
/// The Pin sheet (concept pins spec §3.6): Pin this, Edit pin and New pin open it with four fields, the name, the definition, the words people use and the features,
/// which go into one index.concepts entry of the profile.
/// </summary>
#pragma warning disable MA0182 // red stub (Q7b): the tests use it; the Context page and the shell sheet wire it in the next commits
internal sealed class PinSheetViewModel : ObservableObject
{
    private PinSheetViewModel()
    {
    }

    /// <summary>The pin's name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>The definition line, shown to people and agents.</summary>
    public string Definition { get; private set; } = string.Empty;

    /// <summary>The words people use, best first, each with how many records it would newly link.</summary>
    public IReadOnlyList<PinWordRow> Words { get; private set; } = [];

    /// <summary>The features the pin holds, by exact key.</summary>
    public IReadOnlyList<PinFeatureRow> Features { get; private set; } = [];

    /// <summary>Pin this: the sheet filled from a derived concept's card, its terms and its member features.</summary>
    /// <param name="card">The card Pin this was pressed on.</param>
    /// <param name="terms">The concept's terms with their new-link counts (the suggest read's concept answer), best first.</param>
    /// <param name="features">The concept's member features.</param>
    /// <returns>The sheet.</returns>
    internal static PinSheetViewModel ForCard(ConceptTileViewModel card, IReadOnlyList<PinSuggestTerm> terms, IReadOnlyList<PinSuggestFeature> features) =>
        throw new NotSupportedException();
}
#pragma warning restore MA0182
