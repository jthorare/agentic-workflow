using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>The merge method both merge effects use: the recipe's 'method' argument, else the approved profile's 'workflow.pr.mergeMethod', else squash.</summary>
internal static class PrMergeMethods
{
    /// <summary>Resolves the method.</summary>
    /// <param name="context">The effect's context.</param>
    /// <param name="profile">The approved profile.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The method, or the error.</returns>
    public static async ValueTask<Result<PullMergeMethod>> Resolve(EffectContext context, IApprovedProfile profile, CancellationToken cancellationToken)
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
