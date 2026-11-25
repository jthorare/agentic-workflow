using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>
/// The 'profile' view's one row (keyed by the view's name): the approved and live hashes, when each was seen, and
/// whether (and where) the live profile changed since approval. Before any approval or check: nulls and 'changed: false'.
/// While there is something to approve (nothing approved yet, or the live profile changed) it also carries the plain-words summary of what
/// the approval would allow (setup-once spec §5.2 "Preview"); the repos' file facts come from the cached <see cref="RepoGitCache"/>, so no git runs per request.
/// </summary>
internal sealed class GetProfileService(IViewReader views, IProjectorStatus status, IApprovedProfile approved, IProfileSource live, RepoGitCache git) : IService<GetProfileRequest, GetProfileResponse>
{
    private const double MebiByte = 1024 * 1024;

    public async ValueTask<Result<GetProfileResponse>> Handle(GetProfileRequest request, CancellationToken cancellationToken)
    {
        var asOf = status.Current.AsOf;
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        var row = await views.Get<ProfileRow>(ViewNames.Profile, ViewNames.Profile, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        var changed = row.Value?.Changed == true;
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);   // the file as it is now: one load serves the file hash and the summary
        var fileHash = loaded.IsSuccess ? loaded.Value.Hash : (ProfileHash?)null;
        var summary = latest.IsSuccess && (changed || latest.Value is null) ? await Summarise(latest.Value?.Profile, loaded, cancellationToken).ConfigureAwait(false) : null;
        var editor = EditorOf(loaded);
        return row.Value is { } r
            ? new GetProfileResponse(r.Approved, r.ApprovedAt, r.Live, fileHash, r.CheckedAt, r.Changed, r.ChangedPaths, asOf, summary, editor)
            : new GetProfileResponse(Approved: null, ApprovedAt: null, Live: null, fileHash, CheckedAt: null, Changed: false, ChangedPaths: [], asOf, summary, editor);
    }

    // The workflow.editor key of the live file; null when it is unset or the file does not load.
    private static string? EditorOf(Result<LoadedProfile> loaded) => loaded.IsSuccess ? loaded.Value.Profile.Workflow?.Editor : null;

    // Null when the live profile does not load (the problem shows elsewhere) or asks for nothing new.
    private async ValueTask<ProfileSummary?> Summarise(SindriProfile? approvedProfile, Result<LoadedProfile> loaded, CancellationToken cancellationToken)
    {
        if (loaded.IsError)
        {
            return null;
        }

        var changes = ProfileChanges.Between(approvedProfile, loaded.Value.Profile);
        if (changes.IsEmpty)
        {
            return null;
        }

        var repos = new List<RepoPreview>(changes.NewRepos.Count);
        foreach (var repo in changes.NewRepos)
        {
            var weight = await git.WeightOf(repo.Path, cancellationToken).ConfigureAwait(false);
            var path = weight?.Path ?? repo.Path;
            repos.Add(new RepoPreview(repo.Name, path, weight?.Files ?? 0, Math.Round((weight?.Bytes ?? 0) / MebiByte, 1, MidpointRounding.AwayFromZero), ProfileChanges.RepoSentences(repo.Name, path)));
        }

        return new ProfileSummary(repos, changes.Services, changes.Allowance, changes.Connections, changes.HumanOnly);
    }
}
