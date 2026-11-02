using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The "N new suggested pages" line on the concept cards (<see cref="ContextPageViewModel"/>, continued; pages spec §3.5).</summary>
internal sealed partial class ContextPageViewModel
{
    // The cards' "N new suggested pages" (pages spec §3.5): the seen store is read, pruned and written back in one change; without the preferences port there is no line.
    private async Task<IReadOnlyDictionary<string, int>?> ReadSuggestedSeen(ContextConceptsView view, CancellationToken cancellationToken)
    {
        if (Ports.Prefs is not { } prefs)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        SuggestedPagesSeen.Reading? reading = null;
        await prefs.Update(p =>
        {
            reading = SuggestedPagesSeen.Read(p.SeenPages, _repo ?? string.Empty, view.Concepts ?? []);
            return p with { SeenPages = reading.Seen };
        }).ConfigureAwait(true);
        return reading?.New;
    }

    // The owner opened the concept's Pages section (its scoped map was read): the suggested pages the section shows count as seen, and the card's own keys with them, so a map read
    // newer than the cards marks what the owner saw, not only the cards' older keys (the store's next read forgets any the map dropped). The card loses the line.
    private async Task MarkSuggestedSeen(string conceptId)
    {
        if (Ports.Prefs is not { } prefs || Concepts.Find(conceptId) is not { } tile)
        {
            return;
        }

        var key = SuggestedPagesSeen.KeyOf(_repo ?? string.Empty, conceptId);
        var shown = ShownSuggested(tile);   // read here: the preferences queue runs the change later, off the UI thread
        await prefs.Update(p => p with { SeenPages = SuggestedPagesSeen.Opened(p.SeenPages, key, shown) }).ConfigureAwait(true);
        Concepts.ClearNewSuggested(conceptId);
    }

    // The section's suggested keys first, as the server's list stops at the first 200, then the card's.
    private IReadOnlyList<string> ShownSuggested(ConceptTileViewModel tile) =>
        [.. Whole.Pages.SuggestedKeys.Concat(tile.SuggestedPages).Distinct(StringComparer.Ordinal).Take(UiPreferences.MaxSeenPagesPerConcept)];
}
