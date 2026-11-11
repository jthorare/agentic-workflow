using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetQueue;

/// <summary>
/// The queue (API spec §3.6): the items, open ones only, in one entry per group of <see cref="QueueGroups.Order"/> (an empty
/// group keeps its entry and its sentence, E-18; stale views replace every sentence, the groups' too, and keep the rows), the oldest 'since' first in each. The top-level sentence is the stale-views one, else the queue's.
/// It reads the open states only, one lookup each, pages a group by a (since, id) cursor and tags the answer so an unchanged queue answers 304 (#2788).
/// </summary>
/// <param name="views">The 'items' view.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class GetQueueService(IViewReader views, IProjectorStatus status, ICallerContextAccessor callers) : IService<GetQueueRequest, QueueResponse>
{
    private static readonly ItemState[] _openStates =
        [.. Enum.GetValues<ItemState>().Where(static s => QueueGroups.GroupOf(s) is not null)];

    public async ValueTask<Result<QueueResponse>> Handle(GetQueueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the queue needs a caller");
        }

        var page = ParsePage(request);
        if (page.IsError)
        {
            return page.Error;
        }

        var rows = await ReadOpen(cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var current = status.Current;
        var open = ItemSummaries.Visible(rows.Value, static _ => true);
        var tag = TagOf(open, rows.Value, current, request);
        return request.IfNoneMatch is not null && string.Equals(request.IfNoneMatch, tag, StringComparison.Ordinal)
            ? new QueueResponse([], Empty: null, current.AsOf, Tag: tag, NotModified: true)
            : Answer(open, page.Value, request.Limit, current, tag);
    }

    private static QueueResponse Answer(IReadOnlyList<ItemSummary> open, Page page, int? limit, ViewsStatus current, string tag)
    {
        var byGroup = QueueGroups.Order.ToDictionary(static g => g, g => open.Where(i => QueueGroups.GroupOf(i.State) == g).ToList());
        var groups = QueueGroups.Order.Where(g => page.Only is null || g == page.Only)
            .Select(g => GroupView(g, byGroup[g], page.After, limit, current)).ToList();
        var counts = limit is null && page.Only is null
            ? null
            : new QueueGroupCounts(byGroup[QueueGroup.WaitingOnYou].Count, byGroup[QueueGroup.WithYou].Count, byGroup[QueueGroup.Running].Count, byGroup[QueueGroup.Queued].Count);
        return new QueueResponse(groups, EmptyText.For(current, open.Count == 0, EmptyText.Queue), current.AsOf, counts, tag);
    }

    private static QueueGroupView GroupView(QueueGroup group, List<ItemSummary> all, (DateTimeOffset Since, string Id)? after, int? limit, ViewsStatus current)
    {
        var items = all;
        if (after is { } cursor)
        {
            items = [.. all.Where(i => Compare(i, cursor) > 0)];
        }

        string? next = null;
        if (limit is { } max && items.Count > max)
        {
            items = [.. items.Take(max)];
            next = EncodeCursor(items[^1]);
        }

        IReadOnlyList<ItemSummary> shown = [.. items.Select(static i => i with { Parked = i.State == ItemState.Parked })];
        return new QueueGroupView(group, shown, EmptyText.For(current, all.Count == 0, () => EmptyText.Group(group)), next);
    }

    private static Result<Page> ParsePage(GetQueueRequest request)
    {
        if (request.Limit is <= 0)
        {
            return SindriErrors.InvalidInput.With("limit must be at least 1");
        }

        QueueGroup? only = null;
        if (request.Group is not null)
        {
            only = ParseGroup(request.Group);
            if (only is null)
            {
                return SindriErrors.InvalidInput.With($"'{request.Group}' is not a queue group: waitingOnYou, withYou, running or queued");
            }
        }

        if (request.After is null)
        {
            return new Page(only, After: null);
        }

        return DecodeCursor(request.After) is { } after && only is not null
            ? new Page(only, after)
            : SindriErrors.InvalidInput.With("the cursor is not one this queue gave, or no group names whose page it is");
    }

    private async ValueTask<Result<List<ItemRow>>> ReadOpen(CancellationToken cancellationToken)
    {
        var rows = new List<ItemRow>();
        foreach (var state in _openStates)
        {
            var read = await views.Where<ItemRow>(ViewNames.Items, ViewColumn.State, ItemStateNames.Of(state), cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            rows.AddRange(read.Value);
        }

        return rows;
    }

    private static QueueGroup? ParseGroup(string name) => name.ToLowerInvariant() switch
    {
        "waitingonyou" => QueueGroup.WaitingOnYou,
        "withyou" => QueueGroup.WithYou,
        "running" => QueueGroup.Running,
        "queued" => QueueGroup.Queued,
        _ => null,
    };

    private static int Compare(ItemSummary item, (DateTimeOffset Since, string Id) cursor)
    {
        var bySince = item.Since.CompareTo(cursor.Since);
        return bySince != 0 ? bySince : string.CompareOrdinal(item.Id.Value, cursor.Id);
    }

    private static string EncodeCursor(ItemSummary last)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"{last.Since.UtcTicks}|{last.Id.Value}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static (DateTimeOffset Since, string Id)? DecodeCursor(string cursor)
    {
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var bar = text.IndexOf('|', StringComparison.Ordinal);
            if (bar <= 0 || !long.TryParse(text.AsSpan(0, bar), NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
            {
                return null;
            }

            return (new DateTimeOffset(ticks, offset: TimeSpan.Zero), text[(bar + 1)..]);
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>A hash of what the answer shows: the open rows' ids and sequences, the projector's state word, whether the views are behind, and the paging request; never the cursor or the as-of time.</summary>
    private static string TagOf(IReadOnlyList<ItemSummary> open, List<ItemRow> rows, ViewsStatus current, GetQueueRequest request)
    {
        var sequences = rows.ToDictionary(static r => r.Id.Value, static r => r.Sequence, StringComparer.Ordinal);
        var text = new StringBuilder();
        foreach (var item in open.OrderBy(static i => i.Id.Value, StringComparer.Ordinal))
        {
            _ = text.Append(item.Id.Value).Append(':').Append(sequences[item.Id.Value].ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        var state = current.Stalled ? "stalled" : current.Rebuilding ? "rebuilding" : "ok";
        var behind = current.Cursor < current.LedgerHead;
        _ = text.Append('|').Append(state).Append('|').Append(behind ? "behind" : "current")
            .Append('|').Append(request.Limit?.ToString(CultureInfo.InvariantCulture)).Append('|').Append(request.Group).Append('|').Append(request.After);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }

    private sealed record Page(QueueGroup? Only, (DateTimeOffset Since, string Id)? After);
}
