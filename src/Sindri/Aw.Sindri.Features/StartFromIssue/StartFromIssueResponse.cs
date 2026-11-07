using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.StartFromIssue;

/// <summary>The item the start made and the instance it started.</summary>
/// <param name="Accepted">The command and the ledger sequence of the item's 'item.created'.</param>
/// <param name="Item">The new item.</param>
/// <param name="Instance">The workflow.scoping instance's address.</param>
internal sealed record StartFromIssueResponse(CommandAccepted Accepted, ItemId Item, string Instance);
