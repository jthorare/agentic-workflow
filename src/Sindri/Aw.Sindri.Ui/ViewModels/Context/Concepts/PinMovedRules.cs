using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The §3.8 "What moved" line of a pinned card as one pure function.</summary>
internal static class PinMovedRules
{
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
            features.Add(Labels.PinMovedLost(moved.Lost, moved.To ?? []));
        }

        var joined = string.Join(", ", features);

        // A failed relink is reported even when no feature moved; a running one has nothing to say about records yet.
        if (moved.RelinkFailed)
        {
            return Labels.PinMovedRecordsFailed(joined);
        }

        if (moved.RecordsLinked is not { } records)
        {
            return features.Count == 0 ? null : Labels.PinMovedRecordsLinking(joined);
        }

        if (records != 0)
        {
            features.Add(Labels.PinMovedRecords(records));
        }

        return features.Count == 0 ? null : string.Join(", ", features);
    }
}
