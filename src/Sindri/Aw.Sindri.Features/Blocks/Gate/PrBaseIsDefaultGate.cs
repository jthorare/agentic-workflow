using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>'gate.pr.base-is-default' (PB spec §10.1, BD4): passes only while the PR's base branch is the repo's default branch.</summary>
/// <param name="defaultBranch">The repo's default branch.</param>
internal sealed class PrBaseIsDefaultGate(IDefaultBranch defaultBranch) : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = PrGates.BaseIsDefault;

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
