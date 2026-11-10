using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
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

        var row = resolved.Value;
        var runs = await Runs(row.Id, cancellationToken).ConfigureAwait(false);
        if (runs.IsError)
        {
            return runs.Error;   // an unreadable view is an error, never an empty list: "no runs" and "views broken" must differ
        }

        var launch = await launches.For(row, cancellationToken).ConfigureAwait(false);
        if (launch.IsError)
        {
            return launch.Error;
        }

        return new ItemView(row.Id, row.Title, row.Repo, row.State, row.Step, row.Since, ItemActions.Primary(row.State), ItemActions.For(row.State), row.Owner,
            row.Version, row.CreatedBy, row.WaitingOn, row.Instance, row.CreatedAt, row.UpdatedAt, row.Body, row.Link, runs.Value, launch.Value, asOf);
    }

    private async ValueTask<Result<IReadOnlyList<string>>> Runs(ItemId item, CancellationToken cancellationToken)
    {
        // item-links keys are '<item>/<kind>/<record>': the item's run rows are the keys under '<item>/run/'
        var prefix = item.Value + "/run/";
        var rows = await views.Range<ItemLinkRow>(ViewNames.ItemLinks, prefix, item.Value + "/run0", int.MaxValue, cancellationToken).ConfigureAwait(false);   // '0' follows '/'
        if (rows.IsError)
        {
            return rows.Error;
        }

        return rows.Value
            .Where(static r => r.Kind == ItemLinkKind.Run)
            .OrderByDescending(static r => r.UpdatedAt)
            .ThenByDescending(static r => r.Sequence)
            .Select(static r => r.Ref.StartsWith(RunPrefix, StringComparison.Ordinal) ? r.Ref[RunPrefix.Length..] : r.Ref)
            .ToList();
    }

    private const string RunPrefix = "sindri:run/";
}
