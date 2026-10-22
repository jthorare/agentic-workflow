using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>The recipe stats reads (<see cref="ISindriApi"/>, continued; API P12).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/workflow/recipes?window= (API E-25): the recipe list with each row's stats over the window.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="window">'7d', '30d' or '90d'.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, SND-API-013 for a window outside the set, or SND-UI-002/003.</returns>
    public Task<Result<RecipeList>> RecipesAt(Uri api, string window, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflow/recipes/{id}/stats?window=&amp;hash= (API E-24): one recipe's run figures.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="recipe">The recipe id.</param>
    /// <param name="window">'7d', '30d' or '90d'.</param>
    /// <param name="hash">A hash prefix of eight or more digits to count only the runs under it, or null for every version.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The figures, SND-API-013, SND-RECIPE-002 for an unregistered recipe, or SND-UI-002/003.</returns>
    public Task<Result<RecipeStatsView>> RecipeStats(Uri api, string recipe, string window, string? hash, CancellationToken cancellationToken);
}
