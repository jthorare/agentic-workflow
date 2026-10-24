using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListItems;

/// <summary>
/// The item list (API spec §3.1): the items, filtered by repo and state. Without a state filter only open items show;
/// closed ones appear only when the filter names their state (G11). The oldest 'since' first.
/// </summary>
/// <param name="views">The 'items' view.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class ListItemsService(IViewReader views, IProjectorStatus status, ICallerContextAccessor callers) : IService<ListItemsRequest, ListItemsResponse>
{
    public async ValueTask<Result<ListItemsResponse>> Handle(ListItemsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the item list needs a caller");
        }

        var current = status.Current;
        var rows = await views.List<ItemRow>(ViewNames.Items, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var items = ItemSummaries.Visible(rows.Value, r =>
            (request.State is { } state ? r.State == state : QueueGroups.GroupOf(r.State) is not null)
            && (request.Repo is not { } repo || Equals(r.Repo, repo)));
        return new ListItemsResponse(items, EmptyText.For(current, items.Count == 0, () => EmptyText.Items(request.State, request.Repo)), current.AsOf);
    }
}
