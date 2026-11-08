using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The scope a context Step input reads under (scoping spec C1, C3, C5): the engine as a service, never as the caller, cut to the one repo the
/// instance's item belongs to. The repo comes from <see cref="Core.Workflow.Blocks.StepInputContext.Repo"/> (the ledger), never from a caller.
/// </summary>
/// <param name="approvals">The profile.</param>
internal sealed class ContextInputScopes(IApprovedProfile approvals)
{
    /// <summary>The service scope for <paramref name="repo"/>.</summary>
    /// <param name="repo">The instance's repo.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scope with only that repo, or the error that explains why none can be made (no repo, no profile, repo not in the profile).</returns>
    internal async ValueTask<Result<ContextScope>> For(RepoName? repo, CancellationToken cancellationToken)
    {
        if (repo is not { } name)
        {
            return ContextErrors.RepoOutsideScope.With("the instance's item has no repo, so no repo is mapped for it");
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileMissing.With("no profile yet, so no repo is mapped (fix: sindri repo add <path>)");
        }

        var scope = ContextScopes.For(loaded.Profile, new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service));
        var repos = scope.Repos.Where(r => string.Equals(r.Name.Value, name.Value, StringComparison.Ordinal)).ToArray();
        return repos.Length == 0
            ? ContextErrors.RepoOutsideScope.With($"repo {name.Value} is not in the profile, so nothing is mapped for it")
            : scope with { Repos = repos };
    }
}
