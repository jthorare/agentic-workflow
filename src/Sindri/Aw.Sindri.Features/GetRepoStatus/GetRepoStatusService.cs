using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.GetRepoStatus;

/// <summary>Reads the approved profile and places the directory in it. The live profile only names a repo waiting for approval, so a live profile that can't be read is no name.</summary>
internal sealed class GetRepoStatusService(IApprovedProfile approvals, IProfileSource live, IRepoLocator locator) : IService<GetRepoStatusRequest, GetRepoStatusResponse>
{
    public async ValueTask<Result<GetRepoStatusResponse>> Handle(GetRepoStatusRequest request, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var placed = await locator.Place(request.Directory, approved.Value?.Profile.Repos ?? [], cancellationToken).ConfigureAwait(false);
        if (placed is null)
        {
            return new GetRepoStatusResponse(new RepoStatus(RepoState.OutsideGit, Path: null, Name: null));
        }

        if (approved.Value is null)
        {
            return new GetRepoStatusResponse(new RepoStatus(RepoState.NoApprovedProfile, placed.Path, Name: null));
        }

        if (placed.Repo is { } onboarded)
        {
            return new GetRepoStatusResponse(new RepoStatus(RepoState.Onboarded, placed.Path, onboarded.Value));
        }

        return new GetRepoStatusResponse(new RepoStatus(RepoState.NotOnboarded, placed.Path, await Pending(request.Directory, cancellationToken).ConfigureAwait(false)));
    }

    private async ValueTask<string?> Pending(string directory, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return null;
        }

        return (await locator.Place(directory, loaded.Value.Profile.Repos ?? [], cancellationToken).ConfigureAwait(false))?.Repo?.Value;
    }
}
