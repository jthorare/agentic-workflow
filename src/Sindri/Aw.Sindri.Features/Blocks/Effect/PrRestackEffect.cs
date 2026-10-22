using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'effect.pr.restack' (PB spec §4.6, §10.1): rebases the workspace onto the new base, pushes with a lease on the old head, retargets the pull request.</summary>
internal sealed class PrRestackEffect : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Restack;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
