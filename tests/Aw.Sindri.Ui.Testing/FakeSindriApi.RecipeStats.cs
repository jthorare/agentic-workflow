using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The recipe stats routes of the fake (API P12): the list at a window and one recipe's figures, each answering a golden and recording what it was asked.</summary>
internal sealed partial class FakeSindriApi
{
    /// <summary>GET /sindri/workflow/recipes/{id}/stats' answer.</summary>
    public RouteAnswer RecipeStatsAnswer { get; set; } = RouteAnswer.Api("workflow-recipes-id-stats", "workflow-item");

    /// <summary>Held open until set: a stats read that started while it was set waits for it.</summary>
    public TaskCompletionSource? RecipeStatsGate { get; set; }

    /// <summary>The window of the latest list read; null before the first.</summary>
    public string? LastListWindow { get; private set; }

    /// <summary>The recipe, window and hash of the latest stats read; null before the first.</summary>
    public (string Recipe, string Window, string? Hash)? LastStatsRead { get; private set; }

    public async Task<Result<RecipeList>> RecipesAt(Uri api, string window, CancellationToken cancellationToken)
    {
        LastListWindow = window;
        _ = Interlocked.Increment(ref _recipesCalls);
        await WaitFor(RecipesGate, cancellationToken).ConfigureAwait(false);
        return Answer<RecipeList>(api, WorkflowRoutes.RecipesAt(window), RecipeListAnswer);
    }

    public async Task<Result<RecipeStatsView>> RecipeStats(Uri api, string recipe, string window, string? hash, CancellationToken cancellationToken)
    {
        LastStatsRead = (recipe, window, hash);
        await WaitFor(RecipeStatsGate, cancellationToken).ConfigureAwait(false);
        return Answer<RecipeStatsView>(api, WorkflowRoutes.RecipeStats(recipe, window, hash), RecipeStatsAnswer);
    }
}
