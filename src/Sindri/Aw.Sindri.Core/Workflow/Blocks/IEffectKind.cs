using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// An effect block's runtime (spec §2, §5.7; engine P6): what the 'RunEffect' activity calls once the dispatch delay has
/// passed and the recheck held. It acts once per effect identity: its existence check on
/// <see cref="EffectContext.Identity"/> (or, for 'effect.claim.ref', on the item's claim) makes an activity retry or a
/// 'retry' re-emission harmless, and it then reports <see cref="EffectDone.Existed"/>. Registered with AddEffect.
/// </summary>
public interface IEffectKind
{
    /// <summary>Performs the effect, or finds it already performed.</summary>
    /// <param name="context">The effect, its identity, origin, instance, item and arguments.</param>
    /// <param name="cancellationToken">Cancels the dispatch (the activity's token).</param>
    /// <returns>The outcome its reply event carries, if it has one; an error the activity retries.</returns>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken);
}
