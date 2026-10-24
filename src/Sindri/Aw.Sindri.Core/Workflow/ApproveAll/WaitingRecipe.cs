using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>A shipped recipe the plan names: what changed and the approval it follows (null for a recipe never approved).</summary>
/// <param name="Shipped">The shipped recipe.</param>
/// <param name="Change">What changed since the last approval.</param>
/// <param name="Previous">The latest approved version of the id, or null.</param>
internal sealed record WaitingRecipe(ShippedFile Shipped, PlanChange Change, RecipeApproval? Previous);
