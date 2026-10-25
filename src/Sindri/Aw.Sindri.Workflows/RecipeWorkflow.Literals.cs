using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Common;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

internal sealed partial class RecipeWorkflow
{
    /// <summary>Loads the instance's pinned recipe, then stores its literal inputs.</summary>
    /// <param name="start">The instance's start.</param>
    /// <param name="storesLiterals">Whether this history has the 'child-literal-facts-v1' marker.</param>
    /// <returns>The loaded recipe.</returns>
    private static async Task<LoadedRecipe> LoadRecipe(InstanceStart start, bool storesLiterals)
    {
        var loaded = await Workflow.ExecuteLocalActivityAsync(
            (RunStepActivities a) => a.LoadRecipeAsync(start.Recipe),
            new LocalActivityOptions { StartToCloseTimeout = ActivityBounds.Long }).ConfigureAwait(true);
        await StoreLiterals(start, storesLiterals).ConfigureAwait(true);
        return loaded;
    }

    /// <summary>Writes the instance's literal inputs (a child started with 'literal:' facts) into the fact store, so every reader finds them under their hash.</summary>
    /// <param name="start">The instance's start.</param>
    /// <param name="enabled">Whether this history has the 'child-literal-facts-v1' marker.</param>
    private static async Task StoreLiterals(InstanceStart start, bool enabled)
    {
        FactValue[] literals = enabled ? [.. start.Inputs.Values.Select(static i => i.Value).Where(static v => LiteralFactActivities.IsLiteral(v))] : [];
        if (literals.Length > 0)
        {
            await Workflow.ExecuteLocalActivityAsync(
                (LiteralFactActivities a) => a.StoreAsync(literals),
                new LocalActivityOptions { StartToCloseTimeout = ActivityBounds.Long, RetryPolicy = new RetryPolicy { MaximumAttempts = 3 } }).ConfigureAwait(true);
        }
    }
}
