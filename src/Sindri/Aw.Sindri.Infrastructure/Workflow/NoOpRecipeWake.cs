using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The revoke and drift wake, a stand-in until P6 brings TemporalRecipeWake (the Temporal signal sender; engine spec
/// rev 8, Phase 2): it finds no running instance and sends nothing, so in P4 a revoke or a drift parks none. The
/// approval check at each command, answer and 'complete' still refuses a revoked or drifted recipe.
/// </summary>
internal sealed class NoOpRecipeWake : IRecipeWake
{
    public ValueTask<Result<IReadOnlyList<WorkflowInstanceId>>> Running(RecipeHash hash, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<WorkflowInstanceId>>>(Array.Empty<WorkflowInstanceId>());

    public ValueTask<Result<Unit>> Wake(RecipeWakeNotice notice, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Unit>>(Unit.Value);
}
