using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.purge-marks' (PB spec §5.4, W26, IDEA-033): the babysitter's terminal cleanup. Deletes the comment marks of the pull request 'pr' names. It replies nothing.
/// </summary>
/// <param name="facts">Reads 'pr' back.</param>
/// <param name="marks">The comment marks (W26).</param>
internal sealed class PrPurgeMarksEffect(IFactContents facts, IPrCommentMarks marks) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.PurgeMarks;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
