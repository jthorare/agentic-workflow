using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Launch;

/// <summary>The history of models on a recipe step (model-pick spec §7.2); an adapter reads the 'view_model_history' projection.</summary>
public interface IModelPickHistory
{
    /// <summary>The rows for <paramref name="models"/> on one step; a model with no runs has no row.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="version">The recipe's version.</param>
    /// <param name="state">The recipe state id.</param>
    /// <param name="models">The model ids asked about.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows.</returns>
    public ValueTask<IReadOnlyList<ModelPickHistoryRow>> For(RecipeId recipe, int version, string state, IReadOnlyList<string> models, CancellationToken cancellationToken);
}
