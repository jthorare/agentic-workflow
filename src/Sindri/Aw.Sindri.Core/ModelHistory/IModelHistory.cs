using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.ModelHistory;

/// <summary>Reads the 'model-history' view (model-pick spec §7.2): what the pick learns from how each model has done at a state.</summary>
public interface IModelHistory
{
    /// <summary>The history of each of <paramref name="models"/> at one state of one recipe version.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="version">Its version.</param>
    /// <param name="state">The state.</param>
    /// <param name="models">The model ids.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One row per model, in the order given; a model with no history gets an empty row.</returns>
    public ValueTask<Result<IReadOnlyList<ModelHistoryRow>>> For(RecipeId recipe, int version, string state, IReadOnlyList<string> models, CancellationToken cancellationToken);
}
