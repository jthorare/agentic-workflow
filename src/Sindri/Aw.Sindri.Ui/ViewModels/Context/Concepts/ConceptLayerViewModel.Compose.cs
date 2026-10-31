using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The concept layer (continued): the cards in order with their pin side, and the head line over them (concept pins spec §3.7, §3.8).</summary>
internal sealed partial class ConceptLayerViewModel
{
    private const string OtherKey = "other";

    // Waiting cards first, then pinned cards in profile order, then the cards of removed pins, then the suggestions. Without the pins route the cards are as they were.
    private void Compose()
    {
        if (_pins is not { } pins || _openPin is null)
        {
            Composed = [.. _all.Select(t => t.IsSetByPerson ? t.WithCard(PinCardState.None with { Mark = Labels.ContextConceptSetByPerson }) : t)];
            PinHeadText = string.Empty;
            return;
        }

        var byKey = _all.ToDictionary(t => KeyOf(t.Id), StringComparer.Ordinal);
        var live = pins.Pins.Where(p => !string.Equals(p.State, PinStates.Removing, StringComparison.Ordinal) || byKey.ContainsKey(p.Id)).ToList();
        var waiting = new List<ConceptTileViewModel>();
        var pinned = new List<ConceptTileViewModel>();
        for (var i = 0; i < live.Count; i++)
        {
            var found = byKey.GetValueOrDefault(live[i].Id);
            var card = PinnedCard(live[i], found, [.. live.Take(i)], pins.Rebuild);
            Place(waiting, pinned, found, live[i], card);
        }

        var claimed = live.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var waitingNames = waiting.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var others = _all.Where(t => !claimed.Contains(KeyOf(t.Id))).ToList();
        var removed = others.Where(t => t.IsSetByPerson && !IsOther(t)).Select(t => t.WithCard(RemovedCard(t, pins.Rebuild))).ToList();
        var suggested = others.Where(t => !t.IsSetByPerson && !waitingNames.Contains(t.Name)).Select(Suggested);
        Composed = [.. waiting, .. pinned, .. removed, .. suggested];
        PinHeadText = pinned.Count + waiting.Count + removed.Count == 0 ? Labels.PinNoPinsYet : Coverage(pinned);
    }

    private static void Place(List<ConceptTileViewModel> waiting, List<ConceptTileViewModel> pinned, ConceptTileViewModel? found, PinView pin, PinCardState card)
    {
        if (found is null)
        {
            waiting.Add(WaitingCard(pin, card));
        }
        else
        {
            pinned.Add(found.WithCard(card));
        }
    }

    private PinCardState PinnedCard(PinView pin, ConceptTileViewModel? found, IReadOnlyList<PinView> earlier, MapRebuildView rebuild)
    {
        var footer = PinLineRules.Of(pin, rebuild, PinCoverageRules.Problem(pin, found, earlier, MapFeatures), moved: null, _zone);
        return new PinCardState(Labels.ContextConceptSetByPerson, pin, footer.Lines, Offered(footer.Action), ShowPinThis: false, ShowEditPin: true, Waiting: found is null);
    }

    // A pin the map has no node for yet: a card from the pin itself, which the rebuild replaces with the real one under the same automation id.
    private static ConceptTileViewModel WaitingCard(PinView pin, PinCardState card) =>
        new ConceptTileViewModel("concept:" + pin.Id, pin.Name, pin.Summary ?? string.Empty, new ConceptSize(Features: 0, Records: 0, Decisions: 0, Code: 0), setByPerson: true, pin.Aliases, static _ => { }).WithCard(card);

    // A concept a person set whose pin is gone: it stays as "Unpinned" until the rebuild turns it into a suggestion.
    private PinCardState RemovedCard(ConceptTileViewModel tile, MapRebuildView rebuild)
    {
        var gone = new PinView(KeyOf(tile.Id), tile.Name, tile.Summary, tile.Terms, [], FolderRules: 0, "profile", PinStates.Approved, "removed");
        var footer = PinLineRules.Of(gone, rebuild, problem: null, moved: null, _zone);
        return new PinCardState(Labels.ContextConceptSetByPerson, Pin: null, footer.Lines, Offered(footer.Action), ShowPinThis: false, ShowEditPin: false, Waiting: false);
    }

    // The unassigned concept is no suggestion and takes no pin.
    private static ConceptTileViewModel Suggested(ConceptTileViewModel tile) => IsOther(tile)
        ? tile
        : tile.WithCard(new PinCardState(Labels.PinSuggestedMark, Pin: null, [], PinCardAction.None, ShowPinThis: true, ShowEditPin: false, Waiting: false));

    // The button is offered only when something can answer it: Open review waits for the held-map review to be built.
    private PinCardAction Offered(PinCardAction action) => action switch
    {
        PinCardAction.ReviewAndApprove when ReviewAndApprove is null => PinCardAction.None,
        PinCardAction.OpenReview when OpenReview is null => PinCardAction.None,
        PinCardAction.TryAgain when TryAgain is null => PinCardAction.None,
        PinCardAction.None or PinCardAction.ReviewAndApprove or PinCardAction.OpenReview or PinCardAction.TryAgain => action,
        _ => PinCardAction.None,
    };

    private string Coverage(List<ConceptTileViewModel> pinned) =>
        Labels.PinCoverage(pinned.Sum(t => t.Size.Features), _all.Sum(t => t.Size.Features), pinned.Sum(t => t.Size.Records), _all.Sum(t => t.Size.Records));

    private static bool IsOther(ConceptTileViewModel tile) => string.Equals(KeyOf(tile.Id), OtherKey, StringComparison.Ordinal);

    // "concept:billing" and "billing" are one concept.
    private static string KeyOf(string id) => id.StartsWith("concept:", StringComparison.Ordinal) ? id["concept:".Length..] : id;
}
