using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>'gate.pr.base-is-default' (PB spec §10.1, §10.2, BD4): passes only while the PR's base branch is the repo's default branch, which the forge names.</summary>
/// <param name="pulls">The forge's pull requests.</param>
internal sealed class PrBaseIsDefaultGate(IPullRequests pulls) : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = PrGates.BaseIsDefault;

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
