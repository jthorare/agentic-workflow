using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.ListMessageRows;

/// <summary>
/// The rows of one list, one page at a time (messages spec §8.2): each row classed (§7.2), Needs reply cut to the Release 1 list and ranked, the whole
/// list's count and the key of the next page. A session gets each row's keys, kinds and flags, never the newest message's text (W17).
/// </summary>
/// <param name="store">messages.db.</param>
/// <param name="callers">The caller, for the session filter.</param>
internal sealed class ListMessageRowsService(IMessagesStore store, ICallerContextAccessor callers) : IService<ListMessageRowsRequest, ListMessageRowsResponse>
{
    internal const int MaxLimit = 200;

    // The profile key messages.requestPhrases (§8.5) has no reader yet, so only a '?' makes a direct question.
    private static readonly string[] _requestPhrases = [];

    public async ValueTask<Result<ListMessageRowsResponse>> Handle(ListMessageRowsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rows = await store.Rows(request.List, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var all = Triaged(request.List, rows.Value);
        var start = 0;
        if (request.After is { } after)
        {
            var at = IndexOf(all, after);
            if (at < 0)
            {
                return SindriErrors.InvalidQuery.With($"'after' names no row of the list: {after}; start again from the top");
            }

            start = at + 1;
        }

        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var page = all.Skip(start).Take(limit).ToList();
        var next = start + page.Count < all.Count && page.Count > 0 ? page[^1].Key : null;
        var shown = MessageReadAccess.ShowsText(callers.Current) ? page : [.. page.Select(MessageReadFilter.WithoutText)];
        return new ListMessageRowsResponse(request.List, shown, all.Count, next);
    }

    // Every row gets its class (§7.2), read from the newest text before the session filter drops it. Needs reply keeps the Release 1 list (class 3 and 2,
    // call 21) in rank order; the other lists keep the store's order.
    private static IReadOnlyList<ThreadRow> Triaged(MessageList list, IReadOnlyList<ThreadRow> rows)
    {
        var classed = rows.Select(static r => r with { Class = MessageTriage.Classify(r, _requestPhrases) });
        return list == MessageList.NeedsReply ? MessageRank.Order(classed.Where(MessageTriage.InRelease1NeedsReply)) : [.. classed];
    }

    private static int IndexOf(IReadOnlyList<ThreadRow> rows, string key)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (string.Equals(rows[i].Key, key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
