using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Features.RejectProposal;

/// <summary>The proposal as rejected.</summary>
/// <param name="Proposal">Its summary.</param>
internal sealed record RejectProposalResponse(ProposalSummary Proposal);
