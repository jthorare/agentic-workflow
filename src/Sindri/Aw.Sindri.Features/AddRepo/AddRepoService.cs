using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.AddRepo;

/// <summary>
/// The work of an AddRepo job (setup-once spec §5.1): looks at the folder again (progress line 'checking'), appends the repo to the live profile through the versioned write (the first repo creates the file, with the
/// effects the shipped recipes declare), says 'added to the profile', and records 'repo.added' (the repo's name; the path stays in the non-public payload). The job ends there: the profile waits for the owner's approval as
/// its own step, not as a line of this job.
/// </summary>
/// <param name="resolved">What the start worked out.</param>
/// <param name="inspector">Looks at the folder with git.</param>
/// <param name="writer">Writes the profile.</param>
/// <param name="shipped">The effect kinds a new profile allows.</param>
/// <param name="recorder">Records 'repo.added'.</param>
/// <param name="jobs">The running job: its progress takes the lines.</param>
/// <param name="git">Forgets what it knows of the repos.</param>
internal sealed class AddRepoService(ResolvedRepos resolved, IRepoInspector inspector, IProfileRepoWriter writer, IShippedEffects shipped, RepoEvents recorder, IJobContextAccessor jobs, RepoGitCache git)
    : IService<AddRepoRequest, AddedRepo>
{
    public async ValueTask<Result<AddedRepo>> Handle(AddRepoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!resolved.Take(request, out var repo) || repo is null)
        {
            return SindriErrors.ApiInternal.With("the AddRepo job has no resolved folder");
        }

        if (!recorder.CanRecord())
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");   // before anything is changed
        }

        jobs.Current?.Progress.Report("checking");
        var again = await inspector.Inspect(repo.Inspected.Checkout, cancellationToken).ConfigureAwait(false);
        if (again.IsError)
        {
            return again.Error;
        }

        var written = await writer.Append(new RepoEntry(repo.Name.Value, again.Value.Checkout), shipped.Kinds(), cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        if (written.Value == RepoAppended.AlreadyThere)
        {
            return SindriErrors.SetupRepoExists.With($"that folder is already the repo {repo.Name.Value} (fix: pick another folder)");
        }

        var created = written.Value == RepoAppended.Created;
        return await Record(repo, again.Value.Checkout, created, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<AddedRepo>> Record(ResolvedRepo repo, string checkout, bool created, CancellationToken cancellationToken)
    {
        jobs.Current?.Progress.Report("added to the profile");
        var appended = await recorder.Added(repo.Name, checkout, cancellationToken).ConfigureAwait(false);
        git.Invalidate();
        return appended.IsError ? appended.Error : new AddedRepo(repo.Name, created);
    }
}
