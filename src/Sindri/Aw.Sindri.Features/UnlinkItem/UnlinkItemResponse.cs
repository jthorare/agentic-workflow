using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.UnlinkItem;

/// <summary>The link taken off.</summary>
/// <param name="Accepted">The command and the ledger sequence of the 'item.unlinked'.</param>
/// <param name="Item">The item.</param>
internal sealed record UnlinkItemResponse(CommandAccepted Accepted, ItemId Item);
