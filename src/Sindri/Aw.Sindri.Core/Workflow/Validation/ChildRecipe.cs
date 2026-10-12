using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>A child recipe a 'waitFor: { child }' names, as the resolver found it.</summary>
/// <param name="Recipe">The child's long form.</param>
/// <param name="Hash">Its hash.</param>
public sealed record ChildRecipe(Recipe Recipe, RecipeHash Hash);
