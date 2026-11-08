using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The §3.8 "What moved" line of a pinned card as one pure function.</summary>
internal static class PinMovedRules
{
    private const int MostPlaces = 2;

    /// <summary>The what-moved line.</summary>
    /// <param name="moved">What the last refresh moved in the pin, or null when the map is not that refresh's.</param>
    /// <returns>The line, or null when nothing moved.</returns>
    internal static string? Line(PinMovedView? moved)
    {
        if (moved is null)
        {
            return null;
        }

        var features = new List<string>();
        if (moved.Gained > 0)
        {
            features.Add(Labels.PinMovedGained(moved.Gained));
        }

        if (moved.Lost > 0)
        {
            features.Add(Labels.PinMovedLost(moved.Lost, Places(moved.To)));
        }

        // A failed relink or a running one says nothing about records when no feature moved either: there is nothing to report yet.
        if (moved.RelinkFailed)
        {
            return features.Count == 0 ? null : Labels.PinMovedRecordsFailed(string.Join(", ", features));
        }

        if (moved.RecordsLinked is not { } records)
        {
            return features.Count == 0 ? null : Labels.PinMovedRecordsLinking(string.Join(", ", features));
        }

        if (records != 0)
        {
            features.Add(Labels.PinMovedRecords(records));
        }

        return features.Count == 0 ? null : string.Join(", ", features);
    }

    // Named places first, at most two; features that left into no concept have no name to give.
    private static IReadOnlyList<string> Places(IReadOnlyList<PinMovedTo> to) =>
        [.. to.Select(t => t.Name).OfType<string>().Take(MostPlaces)];
}
