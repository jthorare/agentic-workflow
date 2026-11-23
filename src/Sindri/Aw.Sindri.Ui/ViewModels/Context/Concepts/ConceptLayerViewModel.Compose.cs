using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The concept layer (continued): the cards in order with their pin side, and the head line over them (concept pins spec §3.7, §3.8).</summary>
internal sealed partial class ConceptLayerViewModel
{
    private const string OtherKey = "other";

    // Pinned cards in profile order (a pin the map has no node for is a waiting card in its own slot), then the concepts a person set that no pin names (Pin this is offered), then the suggestions. Without the pins route the cards are as they were.
    private void Compose()
    {
        if (_pins is not { } pins || _openPin is null)
        {
            Composed = [.. _all.Select(t => t.IsSetByPerson ? t.WithCard(PinCardState.None with { Mark = Labels.ContextConceptSetByPerson }) : t)];
            PinHeadText = string.Empty;
            return;
        }

        var byKey = _all.ToDictionary(t => KeyOf(t.Id), StringComparer.Ordinal);
        List<PinView> live = [.. pins.Pins];
        var pinned = new List<ConceptTileViewModel>();
        for (var i = 0; i < live.Count; i++)
        {
            var found = byKey.GetValueOrDefault(live[i].Id);
            var card = PinnedCard(live[i], found, [.. live.Take(i)], pins.Rebuild);
            pinned.Add(found is null ? WaitingCard(live[i], card) : found.WithCard(card));
        }

        var claimed = live.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var waitingNames = pinned.Where(t => t.Card.Waiting).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var others = _all.Where(t => !claimed.Contains(KeyOf(t.Id))).ToList();
        var unpinned = others.Where(t => t.IsSetByPerson && !IsOther(t)).Select(t => t.WithCard(new PinCardState(Labels.ContextConceptSetByPerson, Pin: null, [], PinCardAction.None, ShowPinThis: true, ShowEditPin: false, Waiting: false))).ToList();
        // "Suggested" tells pinned from unpinned, so it reads only when both are on the page (#3677).
        var mixed = pinned.Count > 0;
        var suggested = others.Where(t => !t.IsSetByPerson && !waitingNames.Contains(t.Name)).Select(t => Suggested(t, mixed));
        Composed = [.. pinned, .. unpinned, .. suggested];
        PinHeadText = HeadText(pinned);
    }

    // A removed pin's card stays in place but is no longer pinned: it adds nothing to the count or the coverage.
    private string HeadText(List<ConceptTileViewModel> pinned)
    {
        var counted = pinned.Where(t => t.Card.Pin?.Change != "removed").ToList();
        return counted.Count == 0 ? Labels.PinNoPinsYet : Coverage([.. counted.Where(t => !t.Card.Waiting)]);
    }

    private PinCardState PinnedCard(PinView pin, ConceptTileViewModel? found, IReadOnlyList<PinView> earlier, MapRebuildView rebuild)
    {
        var footer = PinLineRules.Of(pin, rebuild, PinCoverageRules.Problem(pin, found, earlier, MapFeatures), PinMovedRules.Line(pin.Moved), _zone);
        return new PinCardState(Labels.ContextConceptSetByPerson, pin, footer.Lines, Offered(footer.Action), ShowPinThis: false, ShowEditPin: !string.Equals(pin.Change, "removed", StringComparison.Ordinal), Waiting: found is null);
    }

    // A pin the map has no node for yet: a card from the pin itself, which the rebuild replaces with the real one under the same automation id.
    private static ConceptTileViewModel WaitingCard(PinView pin, PinCardState card) =>
        new ConceptTileViewModel("concept:" + pin.Id, pin.Name, pin.Summary ?? string.Empty, new ConceptSize(Features: 0, Records: 0, Decisions: 0, Code: 0), setByPerson: true, pin.Aliases, static _ => { }).WithCard(card);

    // The unassigned concept is no suggestion and takes no pin.
    private static ConceptTileViewModel Suggested(ConceptTileViewModel tile, bool mark) => IsOther(tile)
        ? tile
        : tile.WithCard(new PinCardState(mark ? Labels.PinSuggestedMark : string.Empty, Pin: null, [], PinCardAction.None, ShowPinThis: true, ShowEditPin: false, Waiting: false));

    // The button is offered only when something can answer it: Open review waits for the held-map review to be built.
    private PinCardAction Offered(PinCardAction action) => action switch
    {
        PinCardAction.OpenReview when OpenReview is null => PinCardAction.None,
        PinCardAction.TryAgain when TryAgain is null => PinCardAction.None,
        PinCardAction.None or PinCardAction.OpenReview or PinCardAction.TryAgain => action,
        _ => PinCardAction.None,
    };

    private string Coverage(List<ConceptTileViewModel> pinned) =>
        Labels.PinCoverage(pinned.Sum(t => t.Size.Features), _all.Sum(t => t.Size.Features), pinned.Sum(t => t.Size.Records), _all.Sum(t => t.Size.Records));

    private static bool SameConcept(ConceptTileViewModel left, ConceptTileViewModel right) => string.Equals(KeyOf(left.Id), KeyOf(right.Id), StringComparison.Ordinal);

    private static bool IsOther(ConceptTileViewModel tile) => string.Equals(KeyOf(tile.Id), OtherKey, StringComparison.Ordinal);

    // "concept:billing" and "billing" are one concept.
    private static string KeyOf(string id) => id.StartsWith("concept:", StringComparison.Ordinal) ? id["concept:".Length..] : id;
}
