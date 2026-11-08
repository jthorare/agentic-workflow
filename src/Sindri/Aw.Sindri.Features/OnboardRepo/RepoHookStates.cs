using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.OnboardRepo;

/// <summary><see cref="IRepoHookStates"/> over the approved profile's repos: each repo's pre-commit file, classified by the installer.</summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="installer">Classifies a repo's hook.</param>
internal sealed class RepoHookStates(IApprovedProfile approvals, IPreCommitInstaller installer) : IRepoHookStates
{
    public async ValueTask<IReadOnlyList<RepoHookState>> Read(CancellationToken cancellationToken)
    {
        if (await approvals.Latest(cancellationToken).ConfigureAwait(false) is not { IsSuccess: true, Value: { } approved })
        {
            return [];
        }

        List<RepoHookState> states = [];
        foreach (var entry in approved.Profile.Repos ?? [])
        {
            states.Add(new RepoHookState(entry.Name, entry.Path, await installer.Kind(entry.Path, cancellationToken).ConfigureAwait(false)));
        }

        return states;
    }
}
