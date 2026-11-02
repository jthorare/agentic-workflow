using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// <see cref="IModelPickHistory"/> over the 'model-history' view (model-pick spec 7.2): each model's last 20 outcomes at the step, counted for Jev. A crash is not the model's fault and counts as neither a refusal nor
/// a failed check. A view that can't be read gives no history, so the pick goes on without it.
/// </summary>
/// <param name="history">The view's reader.</param>
internal sealed class ModelPickHistoryAdapter(IModelHistory history) : IModelPickHistory
{
    public async ValueTask<IReadOnlyList<ModelPickHistoryRow>> For(RecipeId recipe, int version, string state, IReadOnlyList<string> models, CancellationToken cancellationToken)
    {
        var read = await history.For(recipe, version, state, models, cancellationToken).ConfigureAwait(false);
        return read.IsError ? [] : [.. read.Value.Select(static r => Count(r))];
    }

    private static ModelPickHistoryRow Count(ModelHistoryRow row) => new(
        row.Model, row.Recent.Count, row.Recent.Count(static o => o == ModelOutcome.Refused), row.Recent.Count(static o => o == ModelOutcome.CheckFailed), row.Recent.Count(static o => o == ModelOutcome.Redraft));
}
