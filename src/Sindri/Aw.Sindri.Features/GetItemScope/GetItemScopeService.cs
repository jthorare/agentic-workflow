using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Engine;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.GetItemScope;

/// <summary>
/// An item's starting points for the Context page. The item's latest 'scoping.approved' row names the packet (hash and instance) and when it was
/// approved; the anchors come from the packet's lenses.json in the instance's kept run area, read through the packet store and checked against the
/// approved hash. An item the caller can't see is SND-ITEM-404. An item with no approval, or one whose packet is gone, changed or unreadable,
/// answers a null scope (the last three with a warning): the lenses are the only source, the scope map is never read.
/// </summary>
/// <param name="items">Resolves the item within the caller's repo scope.</param>
/// <param name="callers">The caller, whose repo scope applies.</param>
/// <param name="ledger">The item's 'scoping.approved' rows.</param>
/// <param name="events">Reads an event's payload.</param>
/// <param name="packets">The kept packet directories.</param>
/// <param name="logger">Where a packet that can't be read is said.</param>
internal sealed partial class GetItemScopeService(IItemResolver items, ICallerContextAccessor callers, ILedgerReader ledger, EventFactory events, IScopePacketStore packets, ILogger<GetItemScopeService> logger)
    : IService<GetItemScopeRequest, ItemScopeResponse>
{
    private const int Page = 100;

    public async ValueTask<Result<ItemScopeResponse>> Handle(GetItemScopeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("a scope read needs a caller");
        }

        var resolved = await items.Resolve(request.Item, caller, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var item = resolved.Value.Id;
        var approved = await Latest(item, cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var scope = approved.Value is { } row ? await Read(item.Value, row, cancellationToken).ConfigureAwait(false) : null;
        return new ItemScopeResponse(item, scope);
    }

    private async ValueTask<Result<LedgerEvent?>> Latest(ItemId item, CancellationToken cancellationToken)
    {
        var filter = new LedgerFilter(TypeFilter.Parse(EventTypes.ScopingApproved).Value, item);
        LedgerEvent? latest = null;
        long cursor = 0;
        while (true)
        {
            var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
            if (head.IsError)
            {
                return head.Error;
            }

            var scan = await ledger.Scan(cursor, Page, filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            latest = scan.Value.Events.Count > 0 ? scan.Value.Events[^1] : latest;
            if (scan.Value.ScannedTo <= cursor || scan.Value.ScannedTo >= head.Value)
            {
                return latest;
            }

            cursor = scan.Value.ScannedTo;
        }
    }

    private async ValueTask<ItemScope?> Read(string item, LedgerEvent row, CancellationToken cancellationToken)
    {
        var approval = events.Payload<ScopingApproved>(row);
        if (approval.IsError || !InstanceAddress.TryFrom(approval.Value.Instance, out var address))
        {
            LogUnreadable(item, row.Sequence, "its approval row does not read");
            return null;
        }

        var instance = WorkflowInstanceId.Of(address);
        var hash = await packets.Hash(instance, cancellationToken).ConfigureAwait(false);
        if (hash.IsError)
        {
            LogUnreadable(item, row.Sequence, string.Equals(hash.Error.Code, SindriErrors.ItemNotFound.Code, StringComparison.Ordinal) ? "its packet is not in the run area" : "its packet could not be hashed");
            return null;
        }

        if (!string.Equals(hash.Value, approval.Value.PacketHash, StringComparison.Ordinal))
        {
            LogUnreadable(item, row.Sequence, "its packet no longer hashes to the approved hash");
            return null;
        }

        var text = await packets.ReadFile(instance, "lenses.json", cancellationToken).ConfigureAwait(false);
        if (text.IsError)
        {
            LogUnreadable(item, row.Sequence, "its lenses.json cannot be read");
            return null;
        }

        if (LensRead.Of(text.Value) is not { } lenses)
        {
            LogUnreadable(item, row.Sequence, "its lenses.json does not read");
            return null;
        }

        return lenses.Scope(approval.Value.PacketHash, row.Envelope.OccurredAt);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "item-scope: {Item} has an approved scoping packet (ledger row {Sequence}) but {Reason}; answering no scope")]
    private partial void LogUnreadable(string item, long sequence, string reason);
}
