using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'effect.pr.merge-docs' (PB spec §4.6, BD9, W23): retargets the docs pull request onto the default branch and merges it with an expected-head check.</summary>
/// <param name="pulls">The pull requests port.</param>
/// <param name="facts">Reads 'pr' back.</param>
/// <param name="profile">The approved profile: the merge method when the recipe names none.</param>
internal sealed class PrMergeDocsEffect(IPullRequests pulls, IFactContents facts, IApprovedProfile profile) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.MergeDocs;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        _ = (pulls, facts, profile);
        throw new NotSupportedException();
    }
}
