using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.CloseItem;

/// <summary>
/// Closes an item as done or cancelled (API spec §3.2, §2.1; desk): resolves it through the views (a human sees every repo), decides on the
/// version it shows, and appends 'item.state-changed' keyed by the item and the version it moves to. Losing the race for that key to another
/// command is SND-ITEM-409. A retried call is a new command, so it meets 'already done' (or the same conflict), never a quiet success.
/// </summary>
/// <param name="items">Resolves the item by id or alias.</param>
/// <param name="ledger">Appends the state change.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller: the command id.</param>
internal sealed class CloseItemService(IItemResolver items, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers)
    : IService<CloseItemRequest, CommandAccepted>
{
    public async ValueTask<Result<CommandAccepted>> Handle(CloseItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("CloseItem needs a caller");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var resolved = await items.Resolve(request.Item, caller, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var decided = ItemDecisions.Close(resolved.Value, request.Reason, request.ItemVersion, caller.CommandId);
        if (decided.IsError)
        {
            return decided.Error;
        }

        var change = decided.Value;
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ItemStateChanged}:{change.Item.Value}:v{change.Version}");
        var entry = events.Entry(EventTypes.ItemStateChanged, change, key, change.Item.Value);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        if (appended.Value.AlreadyRecorded)
        {
            return SindriErrors.ItemConflict.With($"{change.Item.Value} changed since you looked");   // another command already moved it to this version; a retried call has a new command id, so this is never a replay
        }

        return new CommandAccepted(caller.CommandId, appended.Value.Sequence);
    }
}
