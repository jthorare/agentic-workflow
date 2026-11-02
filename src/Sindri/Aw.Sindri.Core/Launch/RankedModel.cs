namespace Aw.Sindri.Core.Launch;

/// <summary>A model with its place in an agent's cheapest-first list and the bands it sits in (model-pick spec §3).</summary>
/// <param name="Model">The model.</param>
/// <param name="Tiers">The bands it sits in.</param>
/// <param name="Unordered">True when its source gave it no place, so it was put last (the probe tells the owner it needs a place in 'workflow.agent.models').</param>
public sealed record RankedModel(ModelFacts Model, ModelTier Tiers, bool Unordered);
