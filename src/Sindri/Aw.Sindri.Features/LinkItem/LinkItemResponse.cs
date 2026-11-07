using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.LinkItem;

/// <summary>The link made.</summary>
/// <param name="Accepted">The command and the ledger sequence of the 'item.linked'.</param>
/// <param name="Item">The item.</param>
internal sealed record LinkItemResponse(CommandAccepted Accepted, ItemId Item);
