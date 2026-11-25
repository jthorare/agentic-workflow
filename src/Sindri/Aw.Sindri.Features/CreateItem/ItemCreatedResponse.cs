using System.Text.Json.Serialization;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>The created item (API spec §3.2 'ItemCreated(accepted, ItemId)'): '{accepted:{commandId, sequence}, itemId}'.</summary>
/// <param name="Accepted">The command and the ledger sequence of its 'item.created'.</param>
/// <param name="ItemId">The new item's id.</param>
/// <param name="Warning">What was not written after the item was made (its link to a run), in plain words; null (and left out of the JSON) when everything was written.</param>
public sealed record ItemCreatedResponse(CommandAccepted Accepted, ItemId ItemId, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Warning = null);
