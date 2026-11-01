using Aw.Sindri.Ui.Api.Context;
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

    // The owner opened the concept's Pages section (its scoped map was read): the concept's current suggested pages count as seen and its card loses the line.
    private async Task MarkSuggestedSeen(string conceptId)
    {
        if (Ports.Prefs is not { } prefs || Concepts.Find(conceptId) is not { } tile)
        {
            return;
        }

        var key = SuggestedPagesSeen.KeyOf(_repo ?? string.Empty, conceptId);
        await prefs.Update(p => p with { SeenPages = SuggestedPagesSeen.Opened(p.SeenPages, key, tile.SuggestedPages) }).ConfigureAwait(true);
        Concepts.ClearNewSuggested(conceptId);
    }
}
