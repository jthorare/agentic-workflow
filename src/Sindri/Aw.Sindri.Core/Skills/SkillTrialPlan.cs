using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Skills;

/// <summary>A block's trial recipe: what <see cref="SkillTrialRecipe.Build"/> made.</summary>
/// <param name="Id">The recipe id, 'workflow.skill-trial-&lt;block&gt;'.</param>
/// <param name="Version">The recipe version.</param>
/// <param name="Text">The recipe file's text.</param>
public sealed record SkillTrialPlan(RecipeId Id, int Version, string Text);
