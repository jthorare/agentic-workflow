using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Features.ListProposals;

/// <summary>The proposals.</summary>
/// <param name="Proposals">One summary per proposal.</param>
internal sealed record ListProposalsResponse(IReadOnlyList<ProposalSummary> Proposals);
