using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>
/// Checks the PR a fix task names (PR review page spec §8.2, §9): <c language="text">review.login</c> is set, the repo is synced (SND-REVIEW-004), Context holds the PR (SND-REVIEW-404), and the forge names its head
/// branch and repository (SND-REVIEW-004 when it can't, since the task couldn't be routed).
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="repos">The synced repos.</param>
/// <param name="facts">The stored PR records.</param>
/// <param name="pulls">The forge port: the PR's head and the credential's own login.</param>
internal sealed class FixTargets(IApprovedProfile approvals, IReviewRepos repos, IReviewFacts facts, IPullRequests pulls)
{
    /// <summary>Resolves the PR.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The target, or the refusal.</returns>
    internal async ValueTask<Result<FixTarget>> Resolve(SendReviewFixRequest request, CancellationToken cancellationToken)
    {
        var slug = $"{request.Owner}/{request.Repo}";
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var profile = approved.Value?.Profile;
        if (profile?.Review?.Login is not { Length: > 0 } login)
        {
            return SindriErrors.ReviewLoginUnset.With("Set your GitHub login on the Reviews tab first.");
        }

        var synced = await repos.Synced(cancellationToken).ConfigureAwait(false);
        if (synced.IsError)
        {
            return synced.Error;
        }

        return synced.Value.Contains(slug, StringComparer.OrdinalIgnoreCase)
            ? await Pull(request, slug, login, RepoOf(profile, request.Repo), cancellationToken).ConfigureAwait(false)
            : SindriErrors.ReviewRepoNotSynced.With($"Sindri doesn't sync {slug}. Add it to context.sources.github.repos.");
    }

    private async ValueTask<Result<FixTarget>> Pull(SendReviewFixRequest request, string slug, string login, RepoName? repo, CancellationToken cancellationToken)
    {
        var read = await facts.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var record = read.Value.Pulls.FirstOrDefault(p => string.Equals(p.Repo, slug, StringComparison.OrdinalIgnoreCase) && p.Number == request.Number);
        if (record?.Url is not { } link)
        {
            return NotFound(slug, request.Number);
        }

        var view = await pulls.Get(link, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error.Code == SindriErrors.ForgeNotFound.Code ? NotFound(slug, request.Number) : view.Error;
        }

        return view.Value is { HeadRef: { Length: > 0 } branch, HeadRepo: { Length: > 0 } headRepo }
            ? new FixTarget(slug, link, headRepo, branch, await PostsThread(record.Author, login, cancellationToken).ConfigureAwait(false), repo)
            : SindriErrors.ReviewRepoNotSynced.With($"{slug}#{request.Number.ToString(CultureInfo.InvariantCulture)}: the forge doesn't name the head branch, so the task can't be routed");
    }

    private static Error NotFound(string slug, int number) => SindriErrors.ReviewPullNotFound.With($"{slug}#{number.ToString(CultureInfo.InvariantCulture)}: PR not found");

    // why: a thread is Sindri's note on the owner's own PR or an agent's (the forge credential's login); on a contributor's PR the task is the item only
    private async ValueTask<bool> PostsThread(string author, string login, CancellationToken cancellationToken)
    {
        if (string.Equals(author, login, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var self = await pulls.Self(cancellationToken).ConfigureAwait(false);
        return self.IsSuccess && string.Equals(author, self.Value, StringComparison.OrdinalIgnoreCase);
    }

    // why: the item belongs to the profile repo the PR's repo names; no match leaves it to the caller's own repo
    private static RepoName? RepoOf(SindriProfile profile, string name) =>
        profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) is { } entry && RepoName.TryFrom(entry.Name, out var repo) ? repo : null;
}
