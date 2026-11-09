using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>One pinned child (spec §4.1): a child is created from the hash its parent's approval pinned.</summary>
/// <param name="Recipe">The child recipe's id.</param>
/// <param name="Hash">The approved child hash the parent pins.</param>
public sealed record ChildPin(RecipeId Recipe, RecipeHash Hash);
