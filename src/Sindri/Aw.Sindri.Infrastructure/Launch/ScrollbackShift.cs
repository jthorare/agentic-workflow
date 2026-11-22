namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// Where the rows of a scrolled-back pane view went once tmux history is full (issue 3829). At its limit tmux drops the oldest rows as new ones arrive, so
/// '#{history_size}' no longer counts the new rows. The capture then holds extra rows above the view, and the previous view's rows found s rows further
/// back in it mean s rows arrived.
/// </summary>
internal static class ScrollbackShift
{
    /// <summary>
    /// The smallest shift s in 0..<paramref name="extra"/> at which <paramref name="previous"/> equals the rows of <paramref name="captured"/> that start at
    /// extra - s, or null when no shift matches. A view of blank rows matches at every shift, so it is 0.
    /// </summary>
    /// <param name="previous">The rows the last capture showed.</param>
    /// <param name="captured">This capture: <paramref name="extra"/> rows above the view, then the view's rows.</param>
    /// <param name="extra">The rows captured above the view.</param>
    /// <returns>The shift, or null.</returns>
    public static int? Find(IReadOnlyList<string> previous, IReadOnlyList<string> captured, int extra) =>
        throw new NotSupportedException();

    /// <summary>
    /// Whether the history is at its limit: tmux trims the oldest tenth of the limit (at least one row) when the history reaches it, so a full history
    /// stays between the limit less that tenth and the limit.
    /// </summary>
    /// <param name="hist">The history size.</param>
    /// <param name="limit">The pane's history-limit.</param>
    /// <returns>True at the limit.</returns>
    public static bool AtLimit(int hist, int limit) => throw new NotSupportedException();
}
