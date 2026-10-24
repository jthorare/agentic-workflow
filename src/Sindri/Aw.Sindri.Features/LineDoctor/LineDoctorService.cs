using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LineDoctor;

/// <summary>
/// 'sindri line doctor &lt;repo&gt;' (line spec Appendix D): places the directory in the approved profile and runs <see cref="LineDoctorRules"/> over it with the box's heavy-slot cap read now.
/// A directory outside git is an invalid input; a repo the profile does not name is one failing row, since nothing else can be checked without its profile.
/// </summary>
internal sealed class LineDoctorService(IApprovedProfile approvals, IRepoLocator locator, IBoxLimitsSource box, ILineSetupProbe probe) : IService<LineDoctorRequest, LineDoctorResponse>
{
    public async ValueTask<Result<LineDoctorResponse>> Handle(LineDoctorRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var placed = await locator.Place(request.Directory, approved.Value?.Profile.Repos ?? [], cancellationToken).ConfigureAwait(false);
        if (placed is null)
        {
            return SindriErrors.InvalidInput.With($"{request.Directory} is not inside a git checkout (fix: pass the repo's path)");
        }

        if (approved.Value is not { } loaded || placed.Repo is not { } repo)
        {
            return new LineDoctorResponse(Repo: null, [new DoctorCheck("repo", CheckState.Fail, $"{placed.Path} is not in the approved profile", "sindri repo onboard " + placed.Path)]);
        }

        var facts = await probe.Read(placed.Path, LineDoctorRules.CredentialVariable(loaded.Profile), cancellationToken).ConfigureAwait(false);
        return new LineDoctorResponse(repo.Value, LineDoctorRules.Check(loaded.Profile, repo.Value, box.Read(), facts));
    }
}
