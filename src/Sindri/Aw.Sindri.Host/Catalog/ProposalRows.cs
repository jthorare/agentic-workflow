using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.RejectProposal;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// Proposals (issue 3787, piece 3): reject is the owner's desk command, refused to an agent session so no agent decides which of its own proposals live.
/// The reads (list, show) are queries and have no row.
/// </summary>
internal static class ProposalRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("RejectProposal"), typeof(RejectProposalRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/proposals/{id}/reject", ShellRows.Form("sindri proposal reject <id>"), "ready", SessionOk: false),
    ];
}
