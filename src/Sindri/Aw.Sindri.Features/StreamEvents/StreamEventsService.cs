using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.StreamEvents;

/// <summary>
/// Reads the public events after the cursor from the ledger (API spec §5.2): the type and item filters run in the ledger scan
/// (S1-D6's engine-key and instance filters here), then <see cref="CallerEventView"/> decides what the caller sees of each row. It reads on,
/// page by page, while scope dropped rows from a full page, until it has <see cref="StreamEventsRequest.Max"/> events or the scan
/// ends, and reports how far it looked, so a filtered stream's cursor moves past the rows it skipped. When a row's scope can't be
/// decided the call keeps what it has and the cursor stays before the row.
/// </summary>
/// <param name="view">What the caller sees of a row.</param>
/// <param name="items">Resolves an item named by alias.</param>
/// <param name="repos">Resolves an item named by id, from the ledger.</param>
/// <param name="callers">The caller, for the item resolution.</param>
internal sealed class StreamEventsService(CallerEventView view, IItemResolver items, IItemRepos repos, ICallerContextAccessor callers)
    : IService<StreamEventsRequest, StreamEventsResponse>
{
    public async ValueTask<Result<StreamEventsResponse>> Handle(StreamEventsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await ItemFilters.Resolve(request.Ref, items, repos, callers, cancellationToken).ConfigureAwait(false);
        if (item.IsError)
        {
            return item.Error;
        }

        var query = new EventQuery(request.After, Math.Clamp(request.Max, 1, 500), new LedgerFilter(request.Types, item.Value), IncludeNonPublic: false, e => Keeps(request, e));
        var page = await view.Read(query, cancellationToken).ConfigureAwait(false);
        return page.IsError ? page.Error : new StreamEventsResponse(page.Value.Events, page.Value.ScannedTo);
    }

    private static bool Keeps(StreamEventsRequest request, LedgerEvent e) =>
        (request.Item is not { } item || string.Equals(e.Item, item.Value, StringComparison.Ordinal))
        && (request.Instance is not { } instance || InstanceEvents.Belongs(e, instance));
}
