using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Review;

/// <summary>The override log as a person reads it (S-4).</summary>
public static class ReviewHistory
{
    /// <summary>The newest overrides a history call returns by default.</summary>
    public const int DefaultLimit = 200;

    /// <summary>The most a history call may ask for.</summary>
    public const int MaxLimit = 10_000;

    /// <summary>Builds the history from every override, newest first.</summary>
    /// <param name="overrides">Every override.</param>
    /// <param name="limit">The most entries to return.</param>
    /// <returns>The history.</returns>
    public static ReviewHistoryView Of(IReadOnlyList<MapOverride> overrides, int limit = DefaultLimit)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        var undone = OverrideLog.Undone(overrides);
        var entries = OverrideLog.Ordered(overrides).Reverse().Select(o =>
        {
            var id = o.ToId();
            return new ReviewHistoryEntry(id, o.Action, o.Target, o.Link, o.Label, o.Who, o.At, undone.Contains(id.Value), o.Undoes);
        }).ToList();
        return new ReviewHistoryView([.. entries.Take(limit)], entries.Count > limit);
    }
}
