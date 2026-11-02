using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.ModelHistory;

/// <summary>A recipe and its version: what the model-history fold remembers of a registered hash and of a started instance.</summary>
/// <param name="Recipe">The recipe.</param>
/// <param name="Version">Its version.</param>
public sealed record RunScope(RecipeId Recipe, int Version);
