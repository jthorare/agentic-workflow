namespace Aw.Sindri.Core.Handoff;

/// <summary>What a won claim returns.</summary>
/// <param name="ClaimKey">The claim's id (the wire field 'claimId'). It is the occurrence of every run the offer starts and the idempotency key of the result.</param>
/// <param name="Offer">The offer, as claimed.</param>
public sealed record ClaimResult(string ClaimKey, Offer Offer);
