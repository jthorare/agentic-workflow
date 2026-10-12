using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>A Step block that reports what it is given: 'step.test.produce'.</summary>
public sealed class FakeStep : IStep, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = TestBlocks.Step("step.test.produce");

    /// <summary>How many runs it started.</summary>
    public int Started { get; private set; }

    /// <inheritdoc/>
    public ValueTask Start(StepContext context, CancellationToken cancellationToken)
    {
        Started++;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<StepResult> Complete(StepRun run, StepReport report, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new StepResult(report.Outcome, report.Outputs));
}
