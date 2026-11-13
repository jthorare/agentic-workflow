using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// What the setup list reads of the profile: the repos the live file names, and whether it is there (a missing file is "no profile", a file with a mistake
/// is an error the list shows as that step's failure).
/// </summary>
/// <param name="live">The live profile file.</param>
/// <param name="git">The repos' git facts, kept 30 seconds, so reading the list runs no git per request.</param>
/// <param name="profileFile">Says whether the profile file is on disk, to tell a new home from a file it can't read</param>
/// <param name="workflows">The workflows, to find hello-sindri.</param>
/// <param name="runs">The workflow runs, to find hello-sindri's succeeded one.</param>
internal sealed class ProfileSetupReader(IProfileSource live, RepoGitCache git, IProfileFile profileFile, IWorkflowStore workflows, IWorkflowRunStore runs)
{
    private const int RunsLooked = 50;
    private static readonly WorkflowSlug _hello = WorkflowSlug.From("hello-sindri");

    internal async ValueTask<Result<RepoFact>> Repos(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return IsAbsent(loaded.Error) ? new RepoFact(0) : Unreadable(loaded.Error);
        }

        var repos = loaded.Value.Profile.Repos ?? [];
        List<SetupItem> items = [];
        foreach (var repo in repos)
        {
            items.Add(new SetupItem(repo.Name, SetupState.Done, GitLine(await git.Of(repo.Path, cancellationToken).ConfigureAwait(false))));
        }

        return new RepoFact(repos.Count) { Items = new SetupItems(items) };
    }

    // The source answers SND-PROFILE-001 for a file it can't read as well as for one that isn't there; only the file probe tells them apart (#3341).
    private bool IsAbsent(Error error) => error.Code == SindriErrors.ProfileMissing.Code && !profileFile.Exists();

    private static Error Unreadable(Error error) => error.Code == SindriErrors.ProfileMissing.Code ? SindriErrors.ProfileInvalid.With("The profile file is there but can't be read; check its permissions.") : error;

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
            // a missing file is "no profile"; a file with a mistake, or one that is there and can't be read, is the step's failure
            return IsAbsent(loaded.Error) ? new ProfileFact(ProfilePresence.Missing) : Unreadable(loaded.Error);
        }

        return new ProfileFact(ProfilePresence.Present) { Preview = ProfilePreview.Lines(loaded.Value.Profile) };
    }

    // The agent the profile's workflows run on (first-run spec §3.1, step 5): 'workflow.agent.target', or none while it is unset or there is no profile.
    internal async ValueTask<Result<AgentFact>> Agent(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error.Code != SindriErrors.ProfileMissing.Code ? loaded.Error : new AgentFact(Target: null);
        }

        return new AgentFact(loaded.Value.Profile.Workflow?.Agent?.Target?.Value);
    }

    // The hello-sindri step (step 8): the dismissal is in the profile ('setup.skipFirstWorkflow'); a succeeded run of hello-sindri comes from the workflow runs.
    internal async ValueTask<Result<FirstWorkflowFact>> FirstWorkflow(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error.Code != SindriErrors.ProfileMissing.Code ? loaded.Error : new FirstWorkflowFact(Succeeded: false, Dismissed: false);
        }

        return new FirstWorkflowFact(await HelloSucceeded(cancellationToken).ConfigureAwait(false), Dismissed: loaded.Value.Profile.Setup?.SkipFirstWorkflow == true);
    }

    // Derived, nothing stored: hello-sindri has a succeeded run. A read that fails counts as "not yet", so the step shows todo and the next read tries again.
    private async ValueTask<bool> HelloSucceeded(CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: false, cancellationToken).ConfigureAwait(false);
        if (listed.IsError || listed.Value.FirstOrDefault(static w => w.Slug == _hello) is not { } hello)
        {
            return false;
        }

        var recent = await runs.Recent(hello.Id, offset: 0, limit: RunsLooked, cancellationToken).ConfigureAwait(false);
        return recent.IsSuccess && recent.Value.Any(static r => r.Status == WorkflowRunStatus.Succeeded);
    }
}
