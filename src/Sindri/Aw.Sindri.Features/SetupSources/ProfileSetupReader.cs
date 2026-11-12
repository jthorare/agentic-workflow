using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// What the setup list reads of the profile: the repos the live file names, and whether it is there (a missing file is "no profile", a file with a mistake
/// is an error the list shows as that step's failure).
/// </summary>
/// <param name="live">The live profile file.</param>
/// <param name="git">The repos' git facts, kept 30 seconds, so reading the list runs no git per request.</param>
internal sealed class ProfileSetupReader(IProfileSource live, RepoGitCache git)
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
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            // a missing file is "no profile"; a file with a mistake, or one that can't be read (SND-PROFILE-004), is the step's failure
            return loaded.Error.Code != SindriErrors.ProfileMissing.Code ? loaded.Error : new ProfileFact(ProfilePresence.Missing);
        }

        return new ProfileFact(ProfilePresence.Present) { Preview = ProfilePreview.Lines(loaded.Value.Profile) };
    }
}
