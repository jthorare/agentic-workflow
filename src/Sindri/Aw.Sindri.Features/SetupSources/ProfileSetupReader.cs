using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// What the setup list reads of the profile: the repos the live file names, and where it stands against its approval (a missing file is "no profile", a file with a mistake
/// is an error the list shows as that step's failure). The live file is judged as `sindri status` judges it, against the approved hash.
/// </summary>
/// <param name="live">The live profile file.</param>
/// <param name="approvals">The approved profile.</param>
/// <param name="git">The repos' git facts, kept 30 seconds, so reading the list runs no git per request.</param>
internal sealed class ProfileSetupReader(IProfileSource live, IApprovedProfile approvals, RepoGitCache git)
{
    internal async ValueTask<Result<RepoFact>> Repos(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error.Code == SindriErrors.ProfileMissing.Code ? new RepoFact(0) : loaded.Error;
        }

        var repos = loaded.Value.Profile.Repos ?? [];
        List<SetupItem> items = [];
        foreach (var repo in repos)
        {
            items.Add(new SetupItem(repo.Name, SetupState.Done, GitLine(await git.Of(repo.Path, cancellationToken).ConfigureAwait(false))));
        }

        return new RepoFact(repos.Count) { Items = new SetupItems(items) };
    }

    // "On main at 1a2b3c4": what git said when it was last asked (at most 30 seconds ago); null when git can't read the folder.
    private static string? GitLine(RepoGit? facts) => facts switch
    {
        null => null,
        { DefaultBranch: { } branch, Head: { } head } => $"On {branch} at {head}",
        { DefaultBranch: { } branch } => $"On {branch}, no commit yet",
        { Head: { } head } => $"At {head}",
        _ => null,
    };

    internal async ValueTask<Result<ProfileFact>> Presence(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            // like status: a live file that doesn't load is judged by the saved approval; a file with a mistake is the step's failure
            return loaded.Error.Code != SindriErrors.ProfileMissing.Code ? loaded.Error : new ProfileFact(approved.Value is null ? ProfilePresence.Missing : ProfilePresence.Approved);
        }

        if (approved.Value is not { } saved)
        {
            return new ProfileFact(ProfilePresence.Unapproved);
        }

        return new ProfileFact(string.Equals(saved.Hash.Value, loaded.Value.Hash.Value, StringComparison.Ordinal) ? ProfilePresence.Approved : ProfilePresence.Drifted);
    }
}
