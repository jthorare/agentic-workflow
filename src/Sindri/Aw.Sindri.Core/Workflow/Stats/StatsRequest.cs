using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>What a stats count was asked for: the recipe, an optional hash scope, the window and the clock.</summary>
/// <param name="Recipe">The recipe.</param>
/// <param name="Hash">The hash asked for, or null.</param>
/// <param name="Window">The window.</param>
/// <param name="Now">The read's time.</param>
internal sealed record StatsRequest(RecipeId Recipe, string? Hash, StatsWindow Window, DateTimeOffset Now);
