namespace Aw.Sindri.Core.Launch;

/// <summary>What a step may and may not use (model-pick spec §4.1). Every member is optional; none set leaves every model a candidate.</summary>
/// <param name="Floor">The recipe's floor as one band ('Cheap', 'Standard' or 'Strong'), or None for no floor.</param>
/// <param name="Cap">The profile's cap as a model id: no model above it. An id the agent doesn't offer is ignored here (profile validation rejects it).</param>
/// <param name="Pin">The profile's pin as a model id: no other model. An id the agent doesn't offer leaves nothing.</param>
/// <param name="Drafter">For a challenger step, the drafter's launch; null for any other step.</param>
/// <param name="Failed">For an escalation, the id of the model that just failed; null for a first run.</param>
public sealed record ModelConstraints(ModelTier Floor = ModelTier.None, string? Cap = null, string? Pin = null, DrafterModel? Drafter = null, string? Failed = null);
