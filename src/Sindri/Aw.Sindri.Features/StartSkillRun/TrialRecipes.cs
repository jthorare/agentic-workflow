using Aw.Kernel;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.RegistryWrites;

namespace Aw.Sindri.Features.StartSkillRun;

/// <summary>Registers a block's trial recipe and approves its hash as the caller: the same two steps 'recipe register' and 'recipe approve' take, in one, because the owner's request to run the block is the approval.</summary>
/// <param name="registrar">Registers recipe text.</param>
/// <param name="planner">Plans the approval.</param>
/// <param name="recorder">Records the approval.</param>
internal sealed class TrialRecipes(RecipeRegistrar registrar, RecipeApprovalPlanner planner, RecipeApprovalRecorder recorder)
{
    /// <summary>Registers the trial recipe and approves its hash as the caller; an approval that already stands is reused.</summary>
    internal async ValueTask<Result<RecipeVersion>> Approve(SkillTrialPlan plan, string actor, CancellationToken cancellationToken)
    {
        var registered = await registrar.Register(plan.Text, plan.Id.Value + ".yaml", cancellationToken).ConfigureAwait(false);
        if (registered.IsError)
        {
            return registered.Error;
        }

        var made = await planner.Approve(registered.Value.Id, registered.Value.Hash, cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        if (!made.Value.AlreadyApproved)
        {
            var recorded = await recorder.Record(made.Value, actor, batch: null, cancellationToken).ConfigureAwait(false);
            if (recorded.IsError)
            {
                return recorded.Error;
            }
        }

        return new RecipeVersion(registered.Value.Id, registered.Value.Version, registered.Value.Hash);
    }
}
