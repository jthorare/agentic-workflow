using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Features.RejectProposal;

/// <summary>The owner turns a proposal down (POST /sindri/proposals/{id}/reject, 'sindri proposal reject'); the same target and title is never saved again.</summary>
/// <param name="Id">The proposal.</param>
internal sealed record RejectProposalRequest(ProposalId Id) : ICommand<RejectProposalResponse>;
