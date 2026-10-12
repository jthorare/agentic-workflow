using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>A gate block that always passes: 'gate.test.pass'.</summary>
public sealed class FakeGate : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = TestBlocks.Gate("gate.test.pass");

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new GateResult(GateVerdict.Pass, Score: null, Reasons: []));
}
