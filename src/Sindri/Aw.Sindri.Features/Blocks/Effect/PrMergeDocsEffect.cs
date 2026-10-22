using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.merge-docs' (PB spec §4.6, BD9, W23): retargets the docs pull request 'pr' names onto the repository's default branch, then merges it with the head the forge
/// shows after the retarget as the expected head. It replies 'pr.docsMerged' with 'merged', 'none' (the pull request is no longer open) or 'pending' (checks not green, a
/// conflict, a head other than the one 'pr' names, or a merge the forge blocked or saw the head move on); it never forces a merge. It reads the checks once: the wait for late
/// checks (workflow.pr.docsMergeWait) and the rebase and secret scan are not done here. The human-trigger rule (W23) is the engine's, not this effect's.
/// </summary>
/// <param name="pulls">The pull requests port.</param>
/// <param name="facts">Reads 'pr' back.</param>
/// <param name="profile">The approved profile: the merge method when the recipe names none.</param>
internal sealed class PrMergeDocsEffect(IPullRequests pulls, IFactContents facts, IApprovedProfile profile) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.MergeDocs;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var pr = await PrReplyFacts.Argument(facts, context, "pr", cancellationToken).ConfigureAwait(false);
        if (pr.IsError)
        {
            return pr.Error;
        }

        var link = PrReplyFacts.Text(pr.Value, "link", "url", "pr");
        if (RepoOf(link) is not { } repo || link is null)
        {
            return WorkflowErrors.NoRuntime.With($"effect.pr.merge-docs of {context.Instance} needs the docs pull request's link ('pr')");
        }

        var method = await PrMergeEffect.Method(context, profile, cancellationToken).ConfigureAwait(false);
        if (method.IsError)
        {
            return method.Error;
        }

        var named = pr.Value.ValueKind == JsonValueKind.Object && pr.Value.TryGetProperty("head", out var head) ? PrReplyFacts.Text(head, "sha") : null;
        return await Merge(new Target(link, repo, named, method.Value), cancellationToken).ConfigureAwait(false);
    }

    private static EffectDone Done(string outcome) => new(OutcomeName.From(outcome), Existed: false);

    // The repository a pull request link names: https://host/owner/name/pull/N.
    private static PullRepo? RepoOf(string? link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? new PullRepo(uri.IdnHost, parts[0], parts[1]) : null;
    }

    private async ValueTask<Result<EffectDone>> Merge(Target target, CancellationToken cancellationToken)
    {
        var before = await pulls.Get(target.Link, cancellationToken).ConfigureAwait(false);
        if (before.IsError)
        {
            return before.Error;
        }

        if (before.Value.State != PullState.Open)
        {
            return Done("none");
        }

        var retargeted = await Retarget(target, cancellationToken).ConfigureAwait(false);
        if (retargeted.IsError)
        {
            return retargeted.Error;
        }

        var now = await pulls.Get(target.Link, cancellationToken).ConfigureAwait(false);
        if (now.IsError)
        {
            return now.Error;
        }

        if (!Ready(now.Value, target.NamedHead))
        {
            return Done("pending");
        }

        var merged = await pulls.Merge(new PullMerge(target.Link, target.Method, now.Value.HeadSha), cancellationToken).ConfigureAwait(false);
        return merged.IsError ? merged.Error : Done(merged.Value.Status == PullMergeStatus.Merged ? "merged" : "pending");
    }

    private async ValueTask<Result<Unit>> Retarget(Target target, CancellationToken cancellationToken)
    {
        var branch = await pulls.DefaultBranch(target.Repo, cancellationToken).ConfigureAwait(false);
        return branch.IsError ? branch.Error : await pulls.Retarget(target.Link, branch.Value, cancellationToken).ConfigureAwait(false);
    }

    // Checks that are green (or none required), no conflict, and the head the argument named (when it named one).
    private static bool Ready(PullView view, string? namedHead) =>
        view.State == PullState.Open
        && view.Mergeable != false
        && view.Checks.State is PullChecksState.Passing or PullChecksState.None
        && (namedHead is null || string.Equals(namedHead, view.HeadSha, StringComparison.Ordinal));

    private sealed record Target(string Link, PullRepo Repo, string? NamedHead, PullMergeMethod Method);
}
