using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>A recipe the reader bound: its long form, the hash of that long form, and where its nodes are in the file.</summary>
/// <param name="Recipe">The normalized long form.</param>
/// <param name="Hash">The hash computed over it (RCP-HASH-001 compares <see cref="Recipe.Hash"/> with this).</param>
/// <param name="Source">The file and its node positions.</param>
public sealed record RecipeDocument(Recipe Recipe, RecipeHash Hash, RecipeSource Source);
