namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// A Step the engine runs itself on the block runtime (DT spec §8.2): a Judge call or a deterministic block. It has no desk
/// report to complete, so it is not an <see cref="IStep"/>; the 'RunBlockStep' activity calls <see cref="Run"/> once (never retried)
/// and the result goes through the same report validation a desk report does. Registered with AddBlockStep.
/// </summary>
public interface IBlockStep
{
    /// <summary>Runs the Step.</summary>
    /// <param name="context">The Step's arguments, resolved from the recipe, and the facts it names.</param>
    /// <param name="cancellationToken">Cancelled when the instance is cancelled or the Step's 'maxDuration' runs out.</param>
    /// <returns>The outcome and the facts it records, or a failure reason.</returns>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken);
}
