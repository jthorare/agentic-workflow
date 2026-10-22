using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.rerun-checks' (PB spec §4.6): asks the forge to run the failed checks again on the commit the snapshot saw them fail on ('fact:prState' checks.sha,
/// else head.sha). A flaky check goes back to 'await' on a step transition, so it replies nothing; asking twice only asks again.
/// </summary>
/// <param name="pulls">The forge port.</param>
/// <param name="contents">Reads the facts it takes.</param>
internal sealed class PrRerunChecksEffect(IPullRequests pulls, IFactContents contents) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.RerunChecks;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var pr = await PrEffectFacts.Content(context, "pr", contents, cancellationToken).ConfigureAwait(false);
        var at = pr.IsError ? pr.Error : await PrEffectFacts.Content(context, "at", contents, cancellationToken).ConfigureAwait(false);
        if (at.IsError)
        {
            return at.Error;
        }

        if (PrEffectFacts.Link(pr.Value) is not { Length: > 0 } link || (PrEffectFacts.ChecksSha(at.Value) ?? PrEffectFacts.Head(at.Value)) is not { Length: > 0 } sha)
        {
            return PrEffectFacts.Missing(context, "fact:pr (a link) and fact:prState (checks.sha or head.sha)");
        }

        var asked = await pulls.RerunChecks(link, sha, cancellationToken).ConfigureAwait(false);
        return asked.IsError ? asked.Error : new EffectDone(Reply: null, Existed: false);
    }
}
