using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Features.GetProposal;

/// <summary>Reads one proposal (GET /sindri/proposals/{id}, 'sindri proposal show').</summary>
/// <param name="Id">The proposal.</param>
internal sealed record GetProposalRequest(ProposalId Id) : IQuery<GetProposalResponse>;
