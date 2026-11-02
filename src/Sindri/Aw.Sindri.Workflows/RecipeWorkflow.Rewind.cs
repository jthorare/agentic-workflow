using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The rewind Update of <see cref="RecipeWorkflow"/> (#1270): 'rewind' re-enters an earlier state with every fact kept, ending a park and
/// replacing a running desk or block run (its activity is abandoned by the same run-token check any transition relies on). A live agent
/// refuses it in the pure decision, so the validator (which runs that decision on the instance as it stands) refuses the Update and records nothing.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    /// <summary>'workflow rewind' (spec §5.2 class desk): the instance re-enters the state the rewind names.</summary>
    /// <param name="rewind">The rewind.</param>
    /// <returns>The instance after it: in the target state with a new run.</returns>
    [WorkflowUpdate("rewind")]
    public Task<CommandResult> RewindAsync(RewindRequested rewind) => Applied(rewind);

    /// <summary>Rejects a rewind the decision refuses (SND-WF-003, the fix naming what would work), recording nothing.</summary>
    /// <param name="rewind">The rewind.</param>
    [WorkflowUpdateValidator(nameof(RewindAsync))]
    public void ValidateRewind(RewindRequested rewind) => Validate(rewind);
}
