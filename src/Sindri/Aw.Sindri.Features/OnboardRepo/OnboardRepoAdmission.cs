using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.OnboardRepo;

/// <summary>
/// The cheap checks an OnboardRepo start makes inside the gate (setup-once spec §5.1): a profile is approved (SND-SETUP-007) and the folder belongs to a repo the approved snapshot names, never the live file (SND-SETUP-004),
/// so both are real HTTP errors before any job is claimed. It keeps the repo it resolved for the job key and the work.
/// </summary>
/// <param name="approvals">The approved profile snapshot.</param>
/// <param name="locator">Places a folder in the snapshot's repos.</param>
/// <param name="resolved">Keeps what was worked out for the job key and the work (the singleton the job registration shares).</param>
internal sealed class OnboardRepoAdmission(IApprovedProfile approvals, IRepoLocator locator, ResolvedOnboardRepos? resolved = null) : IJobAdmission<OnboardRepoRequest>
{
    private readonly ResolvedOnboardRepos _resolved = resolved ?? new ResolvedOnboardRepos();

    public async ValueTask<Result<ServiceName?>> Admit(OnboardRepoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        if (approved.Value is not { } snapshot)
        {
            return SindriErrors.SetupProfileNotApproved.With("no profile is approved yet (fix: sindri profile approve)");
        }

        var repo = await locator.Locate(request.Path, snapshot.Profile.Repos ?? [], cancellationToken).ConfigureAwait(false);
        if (repo is not { } name)
        {
            return SindriErrors.SetupRepoNotInProfile.With("that folder is not in a repo the approved profile names (fix: sindri repo add <path>, approve the profile, then try again)");
        }

        _resolved.Keep(request, name);
        return (ServiceName?)null;
    }

    public void Rollback(OnboardRepoRequest request) => _resolved.Drop(request);
}
