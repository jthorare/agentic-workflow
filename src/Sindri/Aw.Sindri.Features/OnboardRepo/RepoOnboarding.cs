using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.OnboardRepo;

/// <summary>
/// Onboarding after approval (setup-once spec §5.1): the hook slot runs, then 'repo.onboarded' is recorded under the key of the repo and the approved hash, so it is recorded once per approval however often it runs.
/// The setup service calls <see cref="Ensure"/> on every tick; the OnboardRepo job calls <see cref="Onboard"/> for one repo. Neither approves anything.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="hooks">The hook slot.</param>
/// <param name="recorder">Records the event.</param>
/// <param name="known">The (repo, hash) pairs known to be recorded, so a tick does not run them again.</param>
/// <param name="git">Forgets what it knows of the repos.</param>
internal sealed class RepoOnboarding(IApprovedProfile approvals, IRepoHooks hooks, RepoEvents recorder, KnownRepoEvents known, RepoGitCache git) : IRepoOnboardingTick
{
    /// <inheritdoc/>
    public async ValueTask Ensure(CancellationToken cancellationToken)
    {
        if (!recorder.CanRecord() || await approvals.Latest(cancellationToken).ConfigureAwait(false) is not { IsSuccess: true, Value: { } approved })
        {
            return;
        }

        foreach (var entry in approved.Profile.Repos ?? [])
        {
            if (RepoName.TryFrom(entry.Name, out var name) && !known.Has(Marker(name, approved.Hash)))
            {
                _ = await Onboard(approved.Hash, entry, name, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Onboards one repo of the approved profile.</summary>
    /// <param name="hash">The approved profile's hash.</param>
    /// <param name="entry">The repo's entry.</param>
    /// <param name="name">Its name.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>What was done, or why the event could not be recorded.</returns>
    internal async ValueTask<Result<OnboardedRepo>> Onboard(ProfileHash hash, RepoEntry entry, RepoName name, CancellationToken cancellationToken)
    {
        if (!recorder.CanRecord())
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");   // before anything is changed
        }

        var detail = await hooks.Install(entry, cancellationToken).ConfigureAwait(false);
        var appended = await recorder.Onboarded(name, hash, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        known.Add(Marker(name, hash));
        git.Invalidate();
        return new OnboardedRepo(name, detail);
    }

    private static string Marker(RepoName repo, ProfileHash hash) => $"onboarded:{repo.Value}:{hash.Value}";
}
