using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Features.ListProposals;

/// <summary>Lists the evolve proposals, newest change first (GET /sindri/proposals[?state=], 'sindri proposals').</summary>
/// <param name="State">Only this state, or null for all.</param>
internal sealed record ListProposalsRequest(ProposalState? State = null) : IQuery<ListProposalsResponse>;
