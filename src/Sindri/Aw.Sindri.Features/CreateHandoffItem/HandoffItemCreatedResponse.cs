using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.CreateHandoffItem;

/// <summary>The item a handoff made: '{accepted:{commandId, sequence}, itemId, offerKey}'.</summary>
/// <param name="Accepted">The command and the ledger sequence of its 'item.handoff'.</param>
/// <param name="ItemId">The new item's id.</param>
/// <param name="OfferKey">The offer id the item keeps: the sender's, else the item id.</param>
public sealed record HandoffItemCreatedResponse(CommandAccepted Accepted, ItemId ItemId, string OfferKey);
