using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ModelCheck;

// Red stub: nothing registers it until the green commit adds the runtime and its ports' adapters.
#pragma warning disable MA0182

/// <summary>
/// 'step.model.check' (model-check design §3): runs the installed TLC runner over the workspace's spec folders and maps each result line to an outcome: pass, skipped, violation, vacuous,
/// specError, tooBig or cantRun. Not built yet: the red tests name the behaviour.
/// </summary>
/// <param name="folders">Lists the spec folders.</param>
/// <param name="changes">Reads the paths a change touched, for 'scope: touched'.</param>
/// <param name="commands">Runs the runner under one heavy slot.</param>
/// <param name="scrubber">Takes secrets out of a trace.</param>
/// <param name="setup">The runner's path and the environment it gets.</param>
internal sealed class ModelCheckStep(ISpecFolders folders, IBranchChanges changes, RepoCommandRunner commands, ITextScrubber scrubber, ModelCheckSetup setup) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.model.check");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        _ = (folders, changes, commands, scrubber, setup);
        throw new NotSupportedException("step.model.check is not built yet (red)");
    }
}
