using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Items;

namespace Aw.Sindri.Features.CreateHandoffItem;

/// <summary>
/// Makes an item from a handoff (#2258 PR 12): the create-item handler (through <see cref="IItemMaker"/>) (not its command chain: this command already holds the gate), then 'item.handoff' keyed by the item
/// with the whole offer, recorded as the actor 'handoff:&lt;source&gt;'. The item body keeps the first 4,000 characters of the prompt; the event keeps all of it.
/// </summary>
/// <param name="items">The create-item handler.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="ledger">Appends 'item.handoff'.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller: the command id and the actor to replace.</param>
internal sealed class CreateHandoffItemService(IItemMaker items, HeldLease held, ILedgerStore ledger, EventFactory events, ICallerContextAccessor callers)
    : IService<CreateHandoffItemRequest, HandoffItemCreatedResponse>
{
    internal const int MaxBody = 4000;

    public async ValueTask<Result<HandoffItemCreatedResponse>> Handle(CreateHandoffItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("CreateHandoffItem needs a caller");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var body = request.Prompt is { Length: > MaxBody } text ? text[..MaxBody] : request.Prompt;
        var made = await items.Create(request.Title, request.Repo, body, cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        var item = made.Value;
        var offer = request.Offer ?? item.Value;
        var payload = new ItemHandoff(caller.CommandId, item, offer, request.Source, request.Repo ?? caller.Repo, request.Branch, request.Prompt, request.Links ?? [], request.Labels ?? []);
        using var _ = callers.Enter(caller with { Actor = "handoff:" + request.Source });   // the sender is the actor of its own fact
        var entry = events.Entry(EventTypes.ItemHandoff, payload, $"{EventTypes.ItemHandoff}:{item.Value}", item.Value);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new HandoffItemCreatedResponse(new CommandAccepted(caller.CommandId, appended.Value.Sequence), item, offer);
    }
}
