using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.restack' (PB spec §4.6, §10.1): rebases the workspace onto the new base (the commits after the old base's tip, so a stacked pull
/// request drops the ones its parent already merged), pushes the head branch with a lease on the head the snapshot saw, then retargets the pull request
/// when its base changes. It replies 'pr.restacked' with 'restacked', 'nothing' (already on the base), 'conflict' (the rebase was aborted, nothing pushed)
/// or 'refused' (the lease failed or the remote refused the push, nothing retargeted). The secret scan before the push is the recipe's gate.
/// </summary>
/// <param name="pulls">The pull requests port.</param>
/// <param name="rebase">Rebases the workspace.</param>
/// <param name="facts">Reads 'fact:prState' and 'fact:workspace' back.</param>
internal sealed class PrRestackEffect(IPullRequests pulls, IWorkspaceRebase rebase, IFactContents facts) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Restack;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = await RestackPlan.Read(context, facts, cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var moved = await rebase.Rebase(plan.Value.Workspace, plan.Value.Onto, plan.Value.BaseSha, cancellationToken).ConfigureAwait(false);
        if (moved.IsError)
        {
            return moved.Error;
        }

        return moved.Value.Status switch
        {
            RebaseStatus.Conflict => Done("conflict"),
            RebaseStatus.Rebased => await Publish(plan.Value, moved.Value.NewHead!, cancellationToken).ConfigureAwait(false),   // why: a rebased outcome names its tip
            RebaseStatus.Nothing => await Retarget(plan.Value, pushed: false, cancellationToken).ConfigureAwait(false),
            _ => Done("nothing"),
        };
    }

    private async ValueTask<Result<EffectDone>> Publish(RestackPlan plan, string newHead, CancellationToken cancellationToken)
    {
        var push = new PullBranchPush(plan.Repo, plan.Workspace, plan.HeadRef, newHead, plan.HeadSha);
        var pushed = await pulls.PushBranch(push, cancellationToken).ConfigureAwait(false);
        if (pushed.IsError)
        {
            return pushed.Error;
        }

        return pushed.Value == PullPushOutcome.Pushed
            ? await Retarget(plan, pushed: true, cancellationToken).ConfigureAwait(false)
            : Done("refused");
    }

    private async ValueTask<Result<EffectDone>> Retarget(RestackPlan plan, bool pushed, CancellationToken cancellationToken)
    {
        var changes = !string.Equals(plan.BaseRef, plan.Onto, StringComparison.Ordinal);
        if (changes)
        {
            var retargeted = await pulls.Retarget(plan.Link, plan.Onto, cancellationToken).ConfigureAwait(false);
            if (retargeted.IsError)
            {
                return retargeted.Error;
            }
        }

        return Done(pushed || changes ? "restacked" : "nothing");
    }

    private static EffectDone Done(string outcome) => new(OutcomeName.From(outcome), Existed: false);
}
