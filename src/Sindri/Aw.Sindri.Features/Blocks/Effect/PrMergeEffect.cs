using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.merge' (PB spec §10.1, W23): merges the pull request 'pr' names with the forge's expected-head check, the head being the one
/// the snapshot 'at' saw. The method is the recipe's 'method' argument, else the approved profile's 'workflow.pr.mergeMethod', else squash.
/// It replies 'pr.mergeResult' with 'merged', 'blocked' or 'headMoved'; it never forces a merge past a moved head. The human-trigger rule
/// (W23) is the engine's, not this effect's.
/// </summary>
/// <param name="pulls">The pull requests port.</param>
/// <param name="facts">Reads 'pr' and 'at' back.</param>
/// <param name="profile">The approved profile: the merge method when the recipe names none.</param>
internal sealed class PrMergeEffect(IPullRequests pulls, IFactContents facts, IApprovedProfile profile) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Merge;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var pr = await PrReplyFacts.Argument(facts, context, "pr", cancellationToken).ConfigureAwait(false);
        var at = await PrReplyFacts.Argument(facts, context, "at", cancellationToken).ConfigureAwait(false);
        if (pr.IsError || at.IsError)
        {
            return pr.IsError ? pr.Error : at.Error;
        }

        var link = PrReplyFacts.Text(pr.Value, "link", "url", "pr");
        var head = PrReplyFacts.Text(at.Value, "headSha", "head", "sha");
        if (string.IsNullOrEmpty(link) || string.IsNullOrEmpty(head))
        {
            return WorkflowErrors.NoRuntime.With($"effect.pr.merge of {context.Instance} needs the pull request's link ('pr') and the head the snapshot saw ('at': headSha)");
        }

        var method = await Method(context, cancellationToken).ConfigureAwait(false);
        if (method.IsError)
        {
            return method.Error;
        }

        var merged = await pulls.Merge(new PullMerge(link, method.Value, head), cancellationToken).ConfigureAwait(false);
        return merged.IsError ? merged.Error : new EffectDone(Outcome(merged.Value.Status), Existed: false);
    }

    private static OutcomeName Outcome(PullMergeStatus status) => OutcomeName.From(status switch
    {
        PullMergeStatus.Merged => "merged",
        PullMergeStatus.Blocked => "blocked",
        PullMergeStatus.HeadMoved => "headMoved",
        _ => "blocked",   // a status this build doesn't know is not a merge
    });

    private async ValueTask<Result<PullMergeMethod>> Method(EffectContext context, CancellationToken cancellationToken)
    {
        var named = context.Arguments.TryGetValue("method", out var literal) ? PrReplyFacts.Text(literal) : null;
        if (named is null)
        {
            var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
            if (approved.IsError)
            {
                return approved.Error;
            }

            named = approved.Value?.Profile.Workflow?.Pr?.MergeMethod;
        }

        return named?.ToLowerInvariant() switch
        {
            null or "squash" => PullMergeMethod.Squash,
            "merge" => PullMergeMethod.Merge,
            "rebase" => PullMergeMethod.Rebase,
            _ => WorkflowErrors.NoRuntime.With($"merge method '{named}' is not squash, merge or rebase"),
        };
    }
}
