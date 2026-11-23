namespace Aw.Sindri.Core.Evolve;

/// <summary>A proposal's tier and the reason, in fixed words.</summary>
/// <param name="Tier">The tier.</param>
/// <param name="Why">Why, without any text from the proposal.</param>
internal sealed record TierDecision(ProposalTier Tier, string Why);
