using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>Maps an items-view row to the summary the queue and the list share.</summary>
public static class ItemSummaries
{
    /// <summary>The summary of <paramref name="row"/>, with its actions from ItemActions.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The summary.</returns>
    public static ItemSummary From(ItemRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new ItemSummary(row.Id, row.Title, row.Repo, row.State, row.Step, row.Since, ItemActions.Primary(row.State), ItemActions.For(row.State), row.Owner);
    }

    /// <summary>The rows that pass <paramref name="keep"/>, as summaries, the oldest 'since' first (ties by id).</summary>
    /// <param name="rows">The items view.</param>
    /// <param name="keep">Which visible rows to show.</param>
    /// <returns>The summaries, in display order.</returns>
    public static IReadOnlyList<ItemSummary> Visible(IEnumerable<ItemRow> rows, Func<ItemRow, bool> keep)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(keep);
        return [.. rows.Where(keep)
            .OrderBy(static r => r.Since).ThenBy(static r => r.Id.Value, StringComparer.Ordinal)
            .Select(From)];
    }
}
