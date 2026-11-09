using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Handoff;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.CreateHandoffItem;

/// <summary>
/// Makes an item from a handoff (#2258 PR 12): the create-item handler (through <see cref="IItemMaker"/>) (not its command chain: this command already holds the gate), then 'item.handoff' keyed by the item
/// with the whole offer, recorded as the actor 'handoff:&lt;source&gt;' for a human or local caller and as the launched session's own actor for a launched session. The same (source, offerKey) twice makes one item. The item body keeps the first 4,000 characters of the prompt; the event keeps all of it.
/// </summary>
/// <param name="items">The create-item handler.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="ledger">Appends 'item.handoff'.</param>
/// <param name="reader">Finds an earlier handoff of the same (source, offerKey).</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller: the command id and the actor to replace.</param>
internal sealed class CreateHandoffItemService(IItemMaker items, HeldLease held, ILedgerStore ledger, ILedgerReader reader, EventFactory events, ICallerContextAccessor callers)
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

        var key = request.OfferKey is null ? null : $"{EventTypes.ItemHandoff}:{request.Source}:{request.OfferKey}";
        var earlier = key is null ? Results.Success<HandoffItemCreatedResponse?>(null) : await Earlier(key, caller, cancellationToken).ConfigureAwait(false);
        return earlier switch
        {
            { IsError: true } => earlier.Error,
            { Value: { } found } => found,
            _ => await Make(request, caller, grant, key, cancellationToken).ConfigureAwait(false),
        };
    }

    /// <summary>Makes the item and appends its 'item.handoff'. A launched session keeps its own actor; the sender is the actor only for a human or local caller.</summary>
    private async ValueTask<Result<HandoffItemCreatedResponse>> Make(CreateHandoffItemRequest request, CallerContext caller, LeaseGrant grant, string? key, CancellationToken cancellationToken)
    {
        var body = request.Prompt is { Length: > MaxBody } text ? text[..MaxBody] : request.Prompt;
        var made = await items.Create(request.Title, request.Repo, body, cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        var item = made.Value;
        var offer = request.OfferKey ?? item.Value;
        var payload = new ItemHandoff(caller.CommandId, item, offer, request.Source, request.Repo ?? caller.Repo, request.Branch, request.Prompt, request.Links ?? [], request.Labels ?? []);
        using var _ = callers.Enter(caller with { Actor = caller.Launch is not null ? caller.Actor : "handoff:" + request.Source });
        var entry = events.Entry(EventTypes.ItemHandoff, payload, key ?? $"{EventTypes.ItemHandoff}:{item.Value}", item.Value);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        // Lost a race for the same key: the earlier handoff wins (the item made above stays unlinked).
        var winner = appended.Value.AlreadyRecorded && key is not null ? await Earlier(key, caller, cancellationToken).ConfigureAwait(false) : default;
        return winner is { IsError: false, Value: { } first }
            ? first
            : new HandoffItemCreatedResponse(new CommandAccepted(caller.CommandId, appended.Value.Sequence), item, offer);
    }

    private async ValueTask<Result<HandoffItemCreatedResponse?>> Earlier(string key, CallerContext caller, CancellationToken cancellationToken)
    {
        var found = await reader.ByKey(key, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value is not { } recorded)
        {
            return Results.Success<HandoffItemCreatedResponse?>(value: null);
        }

        var earlier = events.Payload<ItemHandoff>(recorded);
        return earlier.IsError
            ? earlier.Error
            : Results.Success<HandoffItemCreatedResponse?>(new HandoffItemCreatedResponse(new CommandAccepted(caller.CommandId, recorded.Sequence), earlier.Value.Item, earlier.Value.OfferKey));
    }
}
