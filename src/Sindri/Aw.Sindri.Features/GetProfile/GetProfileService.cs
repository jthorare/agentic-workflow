using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>
/// The 'profile' view's one row (keyed by the view's name): the approved and live hashes, when each was seen, and
/// whether (and where) the live profile changed since approval. Before any approval or check: nulls and 'changed: false'.
/// While there is something to approve (nothing approved yet, or the live profile changed) it also carries the plain-words summary of what
/// the approval would allow (setup-once spec §5.2 "Preview"); the repos' file facts come from the cached <see cref="RepoGitCache"/>, so no git runs per request.
/// </summary>
internal sealed class GetProfileService(IViewReader views, IProjectorStatus status, ILensPacks lensPacks, IApprovedProfile approved, IProfileSource live, RepoGitCache git, IBlockSentences sentences) : IService<GetProfileRequest, GetProfileResponse>
{
    private const double MebiByte = 1024 * 1024;

    public async ValueTask<Result<GetProfileResponse>> Handle(GetProfileRequest request, CancellationToken cancellationToken)
    {
        var asOf = status.Current.AsOf;
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        var packs = await ResolvePacks(latest.IsError ? null : latest.Value, cancellationToken).ConfigureAwait(false);
        var row = await views.Get<ProfileRow>(ViewNames.Profile, ViewNames.Profile, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        var changed = row.Value?.Changed == true;
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);   // the file as it is now: one load serves the file hash and the summary
        var fileHash = loaded.IsSuccess ? loaded.Value.Hash : (ProfileHash?)null;
        var summary = latest.IsSuccess && (changed || latest.Value is null) ? await Summarise(latest.Value?.Profile, loaded, cancellationToken).ConfigureAwait(false) : null;
        return row.Value is { } r
            ? new GetProfileResponse(r.Approved, r.ApprovedAt, r.Live, fileHash, r.CheckedAt, r.Changed, r.ChangedPaths, asOf, packs, summary)
            : new GetProfileResponse(Approved: null, ApprovedAt: null, Live: null, fileHash, CheckedAt: null, Changed: false, ChangedPaths: [], asOf, packs, summary);
    }

    // Null when the live profile does not load (the problem shows elsewhere) or asks for nothing new.
    private async ValueTask<ProfileSummary?> Summarise(SindriProfile? approvedProfile, Result<LoadedProfile> loaded, CancellationToken cancellationToken)
    {
        if (loaded.IsError)
        {
            return null;
        }

        var changes = ProfileChanges.Between(approvedProfile, loaded.Value.Profile, sentences);
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


    // The default pack of each loop, then every pack a repo entry names: one row per kind and id, in that order.
    private async ValueTask<IReadOnlyList<ProfilePack>> ResolvePacks(LoadedProfile? loaded, CancellationToken cancellationToken)
    {
        var lenses = loaded?.Profile.Workflow?.Packs?.Lenses;
        var packs = new List<ProfilePack>();
        await AddKind(packs, LensPackKind.Design, lenses?.Design, "design-five", cancellationToken).ConfigureAwait(false);
        await AddKind(packs, LensPackKind.Review, lenses?.Review, "code-review", cancellationToken).ConfigureAwait(false);
        return packs;
    }

    private async ValueTask AddKind(List<ProfilePack> packs, LensPackKind kind, PackChoice? choice, string fallback, CancellationToken cancellationToken)
    {
        await Add(packs, kind, repo: null, choice?.Default ?? fallback, cancellationToken).ConfigureAwait(false);
        foreach (var (repo, id) in RepoChoices(choice))
        {
            await Add(packs, kind, RepoName.TryFrom(repo, out var named) ? named : null, id, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> RepoChoices(PackChoice? choice) =>
        choice?.Repos is { } repos ? repos.OrderBy(static r => r.Key, StringComparer.Ordinal) : [];

    private async ValueTask Add(List<ProfilePack> packs, LensPackKind kind, RepoName? repo, string named, CancellationToken cancellationToken)
    {
        var key = kind == LensPackKind.Design ? "lenses.design" : "lenses.review";
        var resolved = await lensPacks.Resolve(kind, repo, cancellationToken).ConfigureAwait(false);
        var pack = resolved.IsError
            ? new ProfilePack(key, LensPackId.TryFrom(named, out var asked) ? asked : null, "built-in", Hash: null, "none", $"invalid: '{named}': {resolved.Error.Message}")
            : new ProfilePack(key, resolved.Value.Id, "built-in", resolved.Value.Hash, "built-in", "ok");
        var same = packs.Exists(p => string.Equals(p.Kind, pack.Kind, StringComparison.Ordinal) && p.Id?.Value == pack.Id?.Value && (pack.Id is not null || p.Status == pack.Status));
        if (!same)
        {
            packs.Add(pack);
        }
    }
}
