using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Worktrees;

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
/// <param name="sandbox">The sandbox checks Onboard reports the blockers of; none means it reports none.</param>
/// <param name="tracked">The files a repo tracks, for the toolchain detector; none means nothing is offered.</param>
internal sealed class RepoOnboarding(IApprovedProfile approvals, IRepoHooks hooks, RepoEvents recorder, KnownRepoEvents known, RepoGitCache git, ISandboxChecks? sandbox = null, ITrackedFiles? tracked = null) : IRepoOnboardingTick
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
        var blockers = sandbox is null ? null : (await sandbox.Run(cancellationToken).ConfigureAwait(false)).Blockers.Select(SandboxBlockerLine.From).ToList();
        var offer = await Offer(entry, cancellationToken).ConfigureAwait(false);
        return new OnboardedRepo(name, detail, blockers, offer.Toolchains.Count == 0 ? null : [.. offer.Toolchains.Select(static t => t.Line())], offer.Settings is { } settings ? SandboxOfferYaml.Render(name.Value, settings) : null);
    }

    // What the repo's files suggest for its 'sandbox.repos.<repo>' entry (sandbox spec §3.7). Only suggested: the profile is the owner's to approve. A repo git can't list offers nothing.
    private async ValueTask<ToolchainOffer> Offer(RepoEntry entry, CancellationToken cancellationToken)
    {
        var listed = tracked is null ? null : await tracked.Paths(entry.Path, cancellationToken).ConfigureAwait(false);
        return listed is { IsSuccess: true } ? SandboxToolchainDetector.Detect(listed.Value) : new ToolchainOffer([], Settings: null);
    }

    private static string Marker(RepoName repo, ProfileHash hash) => $"onboarded:{repo.Value}:{hash.Value}";
}
