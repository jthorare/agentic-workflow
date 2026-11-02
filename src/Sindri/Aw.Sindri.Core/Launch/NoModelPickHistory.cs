using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Launch;

/// <summary>The history of a build with no projection adapter yet: no model has any runs.</summary>
public sealed class NoModelPickHistory : IModelPickHistory
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ModelPickHistoryRow>> For(RecipeId recipe, int version, string state, IReadOnlyList<string> models, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ModelPickHistoryRow>>([]);
}
