using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>The recipe stats reads (<see cref="SindriApiClient"/>, continued; API P12).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<RecipeList>> RecipesAt(Uri api, string window, CancellationToken cancellationToken) =>
        Get<RecipeList>(api, WorkflowRoutes.RecipesAt(window), cancellationToken);

    /// <inheritdoc />
    public Task<Result<RecipeStatsView>> RecipeStats(Uri api, string recipe, string window, string? hash, CancellationToken cancellationToken) =>
        Get<RecipeStatsView>(api, WorkflowRoutes.RecipeStats(recipe, window, hash), cancellationToken);
}
