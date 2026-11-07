namespace Aw.Sindri.Core.Reviews;

/// <summary>One Covers: ref.</summary>
/// <param name="Ref">The ref as written.</param>
/// <param name="State">resolved, broken, inThisPr, inThisPrUnchecked or stale.</param>
/// <param name="Error">Why it is broken.</param>
public sealed record ReviewPageClaim(
    string Ref,
    string State,
    string? Error);
