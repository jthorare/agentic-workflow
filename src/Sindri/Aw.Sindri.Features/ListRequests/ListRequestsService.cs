using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListRequests;

/// <summary>
/// The request list (API spec §3.1, U3-API-4): the requests, by state and
/// by item (an id or alias, resolved by the views), newest first. Stale views replace the empty sentence and keep the rows.
/// </summary>
/// <param name="views">The 'requests' view.</param>
/// <param name="items">Resolves the item filter.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class ListRequestsService(IViewReader views, IItemResolver items, IProjectorStatus status, ICallerContextAccessor callers) : IService<ListRequestsRequest, ListRequestsResponse>
{
    public async ValueTask<Result<ListRequestsResponse>> Handle(ListRequestsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("the request list needs a caller");
        }

        var current = status.Current;
        ItemId? item = null;
        if (request.Item is { } reference)
        {
            var resolved = await items.Resolve(reference, caller, cancellationToken).ConfigureAwait(false);
            if (resolved.IsError)
            {
                return resolved.Error;
            }

            item = resolved.Value.Id;
        }

        var rows = await views.List<RequestRow>(ViewNames.Requests, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var shown = rows.Value
            .Where(r => (request.State is not { } state || r.State == state) && (item is not { } named || r.Item == named))
            .OrderByDescending(static r => r.FiledAt).ThenByDescending(static r => r.Id.Value, StringComparer.Ordinal)
            .Select(static r => new RequestView(r.Id, r.Kind, r.Item, r.Repo, r.Text, r.Actor, r.State, r.Decision, r.FiledAt))
            .ToList();
        return new ListRequestsResponse(shown, EmptyText.For(current, shown.Count == 0, () => EmptyText.Requests(request.State, item)), current.AsOf);
    }
}
