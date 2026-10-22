using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Blocks.Evidence;

/// <summary>'evidence.check' (DT spec §5.5, §8.7): re-runs the frozen reproduction 'fact:check' on the candidate through bugfix-state and reports whether it passes.</summary>
/// <param name="state">bugfix-state.</param>
/// <param name="area">The instance's run area, where its bug fix state lives.</param>
internal sealed class EvidenceCheckProvider(IBugFixState state, IRunArea area) : IEvidenceProvider, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => EvidenceProviders.Check;

    /// <inheritdoc/>
    public ValueTask<EvidenceResult> Collect(EvidenceContext context, CancellationToken cancellationToken)
    {
        _ = (state, area);
        throw new NotSupportedException();
    }
}
