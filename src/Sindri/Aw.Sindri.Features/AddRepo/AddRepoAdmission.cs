using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.AddRepo;

/// <summary>
/// The cheap checks an AddRepo start makes inside the gate (setup-once spec §5.1), before the job is claimed so their codes are real HTTP errors: the name (SND-SETUP-002 when it is not a repo name), the folder
/// (SND-SETUP-001: it exists, is a git checkout, resolves to its main checkout, is readable, is not '/', the home or around or inside the Sindri home), then the live profile (SND-SETUP-002 when the repo is already there
/// or its name is taken). A repo already named whose 'repo.added' is missing gets the event now (the re-run rule). Nothing else is written.
/// </summary>
/// <param name="inspector">Looks at the folder with git.</param>
/// <param name="live">The live profile file.</param>
/// <param name="resolved">Keeps what was worked out for the job.</param>
/// <param name="backfill">Appends a missing 'repo.added'.</param>
internal sealed class AddRepoAdmission(IRepoInspector inspector, IProfileSource live, ResolvedRepos resolved, RepoAddedBackfill backfill) : IJobAdmission<AddRepoRequest>
{
    public async ValueTask<Result<ServiceName?>> Admit(AddRepoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Path) || !Path.IsPathRooted(request.Path))
        {
            return SindriErrors.SetupFolderInvalid.With("the folder must be an absolute path (fix: pick the repo's top folder)");
        }

        var name = NameOf(request);
        if (name.IsError)
        {
            return name.Error;
        }

        var inspected = await inspector.Inspect(request.Path, cancellationToken).ConfigureAwait(false);
        if (inspected.IsError)
        {
            return inspected.Error;
        }

        var taken = await Taken(name.Value, inspected.Value.Checkout, cancellationToken).ConfigureAwait(false);
        if (taken.IsError)
        {
            return taken.Error;
        }

        resolved.Keep(request, new ResolvedRepo(name.Value, inspected.Value));
        return (ServiceName?)null;
    }

    public void Rollback(AddRepoRequest request) => resolved.Drop(request);

    private static Result<RepoName> NameOf(AddRepoRequest request)
    {
        var text = request.Name is { Length: > 0 } given ? given : RepoFolders.NameOf(request.Path);
        return RepoName.TryFrom(text, out var name)
            ? name
            : SindriErrors.SetupRepoExists.With($"'{text}' is not a repo name (fix: use 1 to 100 letters, digits, '.', '_' or '-', starting with a letter or digit)");
    }

    private async ValueTask<Result<Unit>> Taken(RepoName name, string checkout, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error.Code == SindriErrors.ProfileMissing.Code ? Unit.Value : loaded.Error;   // no file yet: the first repo creates it
        }

        foreach (var entry in loaded.Value.Profile.Repos ?? [])
        {
            if (string.Equals(entry.Path.TrimEnd('/'), checkout.TrimEnd('/'), StringComparison.Ordinal))
            {
                await backfill.Ensure(entry, cancellationToken).ConfigureAwait(false);
                return SindriErrors.SetupRepoExists.With($"that folder is already the repo {entry.Name} (fix: pick another folder)");
            }

            if (string.Equals(entry.Name, name.Value, StringComparison.OrdinalIgnoreCase))
            {
                return SindriErrors.SetupRepoExists.With($"the name {name.Value} is taken by another repo (fix: give a different name)");
            }
        }

        return Unit.Value;
    }
}
