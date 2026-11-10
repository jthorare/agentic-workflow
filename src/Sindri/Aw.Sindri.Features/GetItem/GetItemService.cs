using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetItem;

/// <summary>One item (API spec §3.1): resolved by id or alias within the caller's repo scope, with its version, actions, body and link.</summary>
/// <param name="items">Resolves the reference.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller, whose repo scope applies.</param>
/// <param name="launches">The item's launch block.</param>
/// <param name="views">The 'item-links' view, for the runs linked to the item.</param>
internal sealed class GetItemService(IItemResolver items, IProjectorStatus status, ICallerContextAccessor callers, IItemLaunchSource launches, IViewReader views) : IService<GetItemRequest, ItemView>
{
    public async ValueTask<Result<ItemView>> Handle(GetItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("an item needs a caller");
        }

        var asOf = status.Current.AsOf;
        var resolved = await items.Resolve(request.Item, caller, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        ArgumentNullException.ThrowIfNull(views);   // RED stub: Runs is not read yet
        var row = resolved.Value;
        var launch = await launches.For(row, cancellationToken).ConfigureAwait(false);
        if (launch.IsError)
        {
            return launch.Error;
        }

        return new ItemView(row.Id, row.Title, row.Repo, row.State, row.Step, row.Since, ItemActions.Primary(row.State), ItemActions.For(row.State), row.Owner,
            row.Version, row.CreatedBy, row.WaitingOn, row.Instance, row.CreatedAt, row.UpdatedAt, row.Body, row.Link, [], launch.Value, asOf);
    }
}
