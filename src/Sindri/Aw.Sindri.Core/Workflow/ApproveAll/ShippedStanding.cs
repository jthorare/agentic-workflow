using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>Where one shipped recipe stands: revoked (left out of the plan), waiting for an approval, or approved at its shipped hash with live pins.</summary>
/// <param name="Revoked">The shipped hash was revoked.</param>
/// <param name="Waiting">Set when the recipe waits for an approval.</param>
/// <param name="Approval">Set when the recipe stands approved: the approval it stands on.</param>
internal sealed record ShippedStanding(bool Revoked, WaitingRecipe? Waiting, RecipeApproval? Approval);
