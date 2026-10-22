using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.ListRecipes;

/// <summary>The recipes by id and version; empty prints 'no recipes registered'.</summary>
/// <param name="Recipes">The lines, each with its status word and, when the ledger was read, its child-only flag and its stats.</param>
/// <param name="Window">The window the stats cover; left out of the JSON when the stats were not read.</param>
public sealed record ListRecipesResponse(IReadOnlyList<RecipeSummary> Recipes, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Window = null);
