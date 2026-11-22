using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Features.GetProposal;

/// <summary>One proposal with its text. Every text field is model output: a client fences it as untrusted. The source turns are never in it.</summary>
/// <param name="Proposal">Its summary.</param>
/// <param name="Rationale">Why, as drafted.</param>
/// <param name="Change">What to change, as drafted.</param>
/// <param name="Files">The files it names.</param>
/// <param name="Evidence">The opaque refs of the turns behind it.</param>
/// <param name="Sessions">The opaque refs of the sessions it was built from.</param>
/// <param name="Origin">Where it came from.</param>
/// <param name="CreatedAt">When it was first saved.</param>
internal sealed record GetProposalResponse(ProposalSummary Proposal, string Rationale, string Change, IReadOnlyList<string> Files, IReadOnlyList<string> Evidence, IReadOnlyList<string> Sessions, string Origin, DateTimeOffset CreatedAt);
