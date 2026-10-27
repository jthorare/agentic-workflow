using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>One concept as the first layer of the context map shows it: its name, its summary and its size.</summary>
internal sealed class ConceptTileViewModel
{
    private const int TermsShown = 3;

    private readonly Action<string> _open;

    internal ConceptTileViewModel(string id, string name, string summary, ConceptSize size, bool setByPerson, IReadOnlyList<string>? terms, Action<string> open)
    {
        (Id, Name, Summary, _open) = (id, name, summary, open);
        Size = size;
        IsSetByPerson = setByPerson;
        SizeText = Labels.ContextConceptSize(size.Features, size.Records, size.Decisions, size.Code);
        Terms = terms ?? [];
        TermsText = Terms.Count == 0 ? string.Empty : Labels.ContextConceptTerms([.. Terms.Take(TermsShown)], Terms.Count - Math.Min(Terms.Count, TermsShown));
        AccessibleName = Labels.ContextConceptTileName(name, SizeText);
    }

    /// <summary>The concept's id: the value of the map read's concept parameter.</summary>
    public string Id { get; }

    /// <summary>The concept's name.</summary>
    public string Name { get; }

    /// <summary>The summary; empty when the server gave none.</summary>
    public string Summary { get; }

    /// <summary>Whether the summary has words.</summary>
    public bool HasSummary => Summary.Length > 0;

    /// <summary>What the concept holds.</summary>
    internal ConceptSize Size { get; }

    /// <summary>"4 features · 3 records · 2 decisions · 40 code files".</summary>
    public string SizeText { get; }

    /// <summary>Whether a person set this concept, not the model.</summary>
    public bool IsSetByPerson { get; }

    /// <summary>The words that say a person set it.</summary>
    public string SetByPersonText { get; } = Labels.ContextConceptSetByPerson;

    /// <summary>The accessible name.</summary>
    public string AccessibleName { get; }

    /// <summary>The concept's terms, best first; empty until the glossary gives some.</summary>
    internal IReadOnlyList<string> Terms { get; }

    /// <summary>"Terms: a · b · c +2": the first three terms and how many more there are; empty when the server gave none.</summary>
    public string TermsText { get; }

    /// <summary>Whether the card shows terms.</summary>
    public bool HasTerms => TermsText.Length > 0;

    /// <summary>The same card with the terms the glossary gave it.</summary>
    /// <param name="terms">The terms, best first.</param>
    /// <returns>The new card.</returns>
    internal ConceptTileViewModel WithTerms(IReadOnlyList<string> terms) => new(Id, Name, Summary, Size, IsSetByPerson, terms, _open);

    /// <summary>Opens the concept (click, Enter).</summary>
    public void Open() => _open(Id);
}
