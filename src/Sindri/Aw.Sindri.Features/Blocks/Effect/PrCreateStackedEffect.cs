using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'effect.pr.create-stacked' (docs-sync spec §7.2, PB spec §10.1): opens the documentation pull request on top of the code pull request.</summary>
/// <param name="pulls">The forge.</param>
/// <param name="diff">Reads the workspace's head.</param>
/// <param name="contents">Reads the stored facts.</param>
/// <param name="store">Stores 'fact:docsPr'.</param>
internal sealed class PrCreateStackedEffect(IPullRequests pulls, IRunDiff diff, IFactContents contents, IFactStore store)
    : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.CreateStacked;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
