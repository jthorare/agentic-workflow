namespace Aw.Sindri.Core.Launch;

/// <summary>A model a rule took out, for the SND-MODEL-003 detail (model-pick spec §12).</summary>
/// <param name="Model">The model.</param>
/// <param name="Constraint">The first rule that removed it.</param>
public sealed record ModelRemoval(RankedModel Model, ModelConstraint Constraint);
