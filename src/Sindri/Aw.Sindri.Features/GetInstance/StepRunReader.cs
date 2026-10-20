using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>
/// An instance's Step runs: its ledger events (<see cref="IStepRunSource"/>) folded with the loop caps of its recipe. A
/// recipe that can't be read now leaves the runs' max attempts null; a ledger that can't be read fails the read.
/// </summary>
/// <param name="source">The instance's workflow events.</param>
/// <param name="recipes">The approved recipes, whose loops give each run its max attempts.</param>
internal sealed class StepRunReader(IStepRunSource source, IApprovedRecipes recipes)
{
    /// <summary>The Step runs of <paramref name="view"/>'s instance, in start order.</summary>
    /// <param name="view">The instance's view, whose recipe and state cells give the caps.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs, or the ledger's error.</returns>
    public async ValueTask<Result<IReadOnlyList<StepRun>>> Read(InstanceView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        var recorded = await source.EventsOf(view.Address, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        var approved = await recipes.Find(view.Recipe.Id, view.Recipe.Hash, cancellationToken).ConfigureAwait(false);
        var caps = approved.IsSuccess ? StepRunCaps.Of(approved.Value.Recipe, view.States) : new Dictionary<string, int>(StringComparer.Ordinal);
        return Results.Success(StepRuns.Fold(recorded.Value, caps));
    }
}
