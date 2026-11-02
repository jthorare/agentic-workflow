using Aw.Kernel;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>The 'model-history' view read through the views.db reader (model-pick spec §7.2).</summary>
/// <param name="reader">The views.db read side.</param>
internal sealed class SqliteModelHistory(IViewReader reader) : IModelHistory
{
    public async ValueTask<Result<IReadOnlyList<ModelHistoryRow>>> For(RecipeId recipe, int version, string state, IReadOnlyList<string> models, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(models);
        var rows = new List<ModelHistoryRow>(models.Count);
        foreach (var model in models)
        {
            var found = await reader.Get<ModelHistoryRow>(ViewNames.ModelHistory, ModelHistoryKeys.For(recipe, version, state, model), cancellationToken).ConfigureAwait(false);
            if (found.IsError)
            {
                return found.Error;
            }

            rows.Add(found.Value ?? ModelHistoryRow.Empty(recipe, version, state, model));
        }

        return rows;
    }
}
