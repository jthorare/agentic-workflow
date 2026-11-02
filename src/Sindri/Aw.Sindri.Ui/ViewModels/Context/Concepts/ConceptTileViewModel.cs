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

    /// <summary>How many pages express the concept; 0 when the server does not count them.</summary>
    internal int Pages { get; init; }

    /// <summary>The map keys of the concept's suggested pages, as the server listed them; empty when none.</summary>
    internal IReadOnlyList<string> SuggestedPages { get; init; } = [];

    /// <summary>How many suggested pages a rebuild found that the owner has not opened yet.</summary>
    internal int NewSuggested { get; init; }

    /// <summary>"2 new suggested pages"; empty when none is new.</summary>
    public string NewSuggestedText => NewSuggested > 0 ? Labels.ContextConceptNewSuggested(NewSuggested) : string.Empty;

    /// <summary>Whether the card shows the new suggested pages line.</summary>
    public bool HasNewSuggested => NewSuggested > 0;

    /// <summary>The same card with its new suggested pages line gone (the owner opened the concept's Pages section).</summary>
    /// <returns>The new card.</returns>
    internal ConceptTileViewModel WithoutNewSuggested() => new(Id, Name, Summary, Size, IsSetByPerson, Terms, _open) { Card = Card, Pages = Pages, SuggestedPages = SuggestedPages };

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
    internal ConceptTileViewModel WithTerms(IReadOnlyList<string> terms) => new(Id, Name, Summary, Size, IsSetByPerson, terms, _open) { Card = Card, Pages = Pages, SuggestedPages = SuggestedPages, NewSuggested = NewSuggested };

    /// <summary>The pin side of the card: its mark, footer and buttons.</summary>
    internal PinCardState Card { get; private init; } = PinCardState.None;

    /// <summary>The automation id of a pinned or waiting card ("PinCard.billing"); empty on a derived one.</summary>
    public string AutomationId => Card.Pin is { } pin ? "PinCard." + pin.Id : string.Empty;

    /// <summary>"Set by a person" or "Suggested"; empty when the server has no pins.</summary>
    public string MarkText => Card.Mark;

    /// <summary>Whether the card has a mark.</summary>
    public bool HasMark => Card.Mark.Length > 0;

    /// <summary>The footer's lines, one per row.</summary>
    public IReadOnlyList<string> Lines => Card.Lines;

    /// <summary>Whether the card has a footer.</summary>
    public bool HasFooter => Card.Lines.Count > 0;

    /// <summary>Whether the footer's button needs the owner (Review and approve, Open review): the card stands out.</summary>
    public bool NeedsYou => Card.Action is PinCardAction.ReviewAndApprove or PinCardAction.OpenReview;

    /// <summary>The footer button's words; empty when it has none.</summary>
    public string ActionText => Card.Action switch
    {
        PinCardAction.ReviewAndApprove => Labels.PinReviewAndApprove,
        PinCardAction.OpenReview => Labels.PinOpenReview,
        PinCardAction.TryAgain => Labels.PinTryAgain,
        PinCardAction.None => string.Empty,
        _ => string.Empty,
    };

    /// <summary>Whether the footer carries a button.</summary>
    public bool HasAction => Card.Action != PinCardAction.None;

    /// <summary>Whether the card carries Pin this.</summary>
    public bool ShowPinThis => Card.ShowPinThis;

    /// <summary>Whether the card carries Edit pin.</summary>
    public bool ShowEditPin => Card.ShowEditPin;

    /// <summary>The Pin this button's words.</summary>
    public string PinThisText { get; } = Labels.PinThis;

    /// <summary>The Edit pin button's words.</summary>
    public string EditPinText { get; } = Labels.PinEdit;

    /// <summary>The same card with the pin side the layer worked out.</summary>
    /// <param name="card">The pin side.</param>
    /// <returns>The new card.</returns>
    internal ConceptTileViewModel WithCard(PinCardState card) => new(Id, Name, Summary, Size, IsSetByPerson, Terms, _open) { Card = card, Pages = Pages, SuggestedPages = SuggestedPages, NewSuggested = NewSuggested };

    /// <summary>Opens the concept (click, Enter).</summary>
    public void Open() => _open(Id);
}
