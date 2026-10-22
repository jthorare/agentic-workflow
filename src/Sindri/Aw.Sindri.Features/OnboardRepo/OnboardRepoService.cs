using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.OnboardRepo;

/// <summary>
/// The work of an OnboardRepo job (setup-once spec §5.1): the hook slot, then 'repo.onboarded' for the approved hash. The profile is read again, so a profile approved a moment ago is the one recorded; it is never approved here.
/// </summary>
/// <param name="resolved">What the start worked out.</param>
/// <param name="approvals">The approved profile.</param>
/// <param name="onboarding">Onboards one repo.</param>
/// <param name="jobs">The running job: its progress takes the lines.</param>
internal sealed class OnboardRepoService(ResolvedOnboardRepos resolved, IApprovedProfile approvals, RepoOnboarding onboarding, IJobContextAccessor jobs) : IService<OnboardRepoRequest, OnboardedRepo>
{
    public async ValueTask<Result<OnboardedRepo>> Handle(OnboardRepoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (resolved.Take(request) is not { } repo)
        {
            return SindriErrors.ApiInternal.With("the OnboardRepo job has no resolved repo");
        }

        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var entry = latest.Value?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, repo.Value, StringComparison.Ordinal));
        if (latest.Value is not { } approved || entry is null)
        {
            return SindriErrors.SetupRepoNotInProfile.With($"'{repo.Value}' is no longer in the approved profile (fix: approve the profile, then try again)");
        }

        jobs.Current?.Progress.Report("onboarding");
        return await onboarding.Onboard(approved.Hash, entry, repo, cancellationToken).ConfigureAwait(false);
    }
}
