using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.ListMessageRows;

/// <summary>
/// The rows of one list, one page at a time (messages spec §8.2): the store's order, the whole list's count and the key of the next page. A session gets
/// each row's keys, kinds and flags, never the newest message's text (W17).
/// </summary>
/// <param name="store">messages.db.</param>
/// <param name="callers">The caller, for the session filter.</param>
internal sealed class ListMessageRowsService(IMessagesStore store, ICallerContextAccessor callers) : IService<ListMessageRowsRequest, ListMessageRowsResponse>
{
    internal const int MaxLimit = 200;

    public async ValueTask<Result<ListMessageRowsResponse>> Handle(ListMessageRowsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rows = await store.Rows(request.List, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var all = rows.Value;
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
        var shown = MessageReadFilter.ShowsText(callers.Current) ? page : [.. page.Select(MessageReadFilter.WithoutText)];
        return new ListMessageRowsResponse(request.List, shown, all.Count, next);
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
