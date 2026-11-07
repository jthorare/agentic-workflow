using Aw.Kernel;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.RegistryWrites;

namespace Aw.Sindri.Features.StartSkillRun;

/// <summary>Registers a block's trial recipe and approves its hash as the caller: the same two steps 'recipe register' and 'recipe approve' take, in one, because the owner's request to run the block is the approval.</summary>
/// <param name="store">The registered versions of the trial's id.</param>
/// <param name="reader">Hashes the trial's text at a registered version.</param>
/// <param name="registrar">Registers recipe text.</param>
/// <param name="planner">Plans the approval.</param>
/// <param name="recorder">Records the approval.</param>
internal sealed class TrialRecipes(IRecipeStore store, IRecipeReader reader, RecipeRegistrar registrar, RecipeApprovalPlanner planner, RecipeApprovalRecorder recorder)
{
    /// <summary>Registers the trial recipe at its version (<see cref="SkillTrialRecipe.VersionFor"/>) and approves its hash as the caller; an approval that already stands is reused.</summary>
    /// <param name="id">The trial's recipe id.</param>
    /// <param name="at">The trial's recipe at a version.</param>
    /// <param name="actor">The approver.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    internal async ValueTask<Result<RecipeVersion>> Approve(RecipeId id, Func<int, SkillTrialPlan> at, string actor, CancellationToken cancellationToken)
    {
        var versions = await store.Versions(id, cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return versions.Error;
        }

        var plan = at(SkillTrialRecipe.VersionFor(versions.Value, version => reader.Read(at(version).Text, id.Value + ".yaml").Document?.Hash));
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
