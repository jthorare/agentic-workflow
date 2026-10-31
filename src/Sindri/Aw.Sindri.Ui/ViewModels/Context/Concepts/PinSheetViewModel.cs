using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>
/// The Pin sheet (concept pins spec §3.6): Pin this, Edit pin and New pin open it with four fields, the name, the definition, the words people use and the features,
/// which go into one index.concepts entry of the profile.
/// </summary>
#pragma warning disable MA0182 // Q7b: the tests use it; the Context page and the shell sheet wire it in the next commits
internal sealed class PinSheetViewModel : ObservableObject
{
    /// <summary>The most words the sheet fills in from a concept's terms (§3.6).</summary>
    internal const int WordsShown = 20;

    /// <summary>A word that would newly link more records than this starts unticked, likely too broad (§3.6).</summary>
    internal const int TooBroad = 200;

    private PinSheetViewModel(string name, string definition, IReadOnlyList<PinWordRow> words, IReadOnlyList<PinFeatureRow> features)
    {
        (Name, Definition, Words, Features) = (name, definition, words, features);
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
        new(
            card.Name,
            card.Summary,
            [.. terms.Take(WordsShown).Select(t => new PinWordRow(t.Word, t.NewRecords, Ticked: t.NewRecords <= TooBroad))],
            [.. features.Select(f => new PinFeatureRow(f.Key, f.Label, Ticked: true))]);
}
#pragma warning restore MA0182
