using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>One child a recipe uses, in the approve preview (#1042): the version the approval pins and, when this build ships a newer one that waits, a warning.</summary>
/// <param name="Recipe">The child's id.</param>
/// <param name="PinnedHash">The hash the approval pins.</param>
/// <param name="PinnedVersion">Its version, or null when the registry does not hold it.</param>
/// <param name="NewerHash">The hash of the newer shipped version that waits for its approval, or null.</param>
/// <param name="NewerVersion">That version, or null.</param>
/// <param name="Note">The sentence the preview prints.</param>
public sealed record PreviewChildNote(RecipeId Recipe, RecipeHash PinnedHash, int? PinnedVersion, RecipeHash? NewerHash, int? NewerVersion, string Note);
