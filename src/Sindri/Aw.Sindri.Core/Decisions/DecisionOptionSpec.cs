using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Decisions;

/// <summary>One option a decision offers (U3-API-3, -6).</summary>
/// <param name="Id">The option, what 'Decide' takes as its choice.</param>
/// <param name="Label">What the button says.</param>
/// <param name="RejectLike">True for the option the X key picks (Decline; owner decision of 2026-10-14).</param>
public sealed record DecisionOptionSpec(DecisionOption Id, string Label, bool RejectLike);
