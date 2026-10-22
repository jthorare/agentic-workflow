using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>
/// The 'profile' view's one row (keyed by the view's name): the approved and live hashes, when each was seen, and
/// whether (and where) the live profile changed since approval. Before any approval or check: nulls and 'changed: false'.
/// </summary>
internal sealed class GetProfileService(IViewReader views, IProjectorStatus status, ILensPacks lensPacks, IApprovedProfile approved) : IService<GetProfileRequest, GetProfileResponse>
{
    public async ValueTask<Result<GetProfileResponse>> Handle(GetProfileRequest request, CancellationToken cancellationToken)
    {
        var asOf = status.Current.AsOf;
        var packs = await ResolvePacks(cancellationToken).ConfigureAwait(false);
        var row = await views.Get<ProfileRow>(ViewNames.Profile, ViewNames.Profile, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        return row.Value is { } r
            ? new GetProfileResponse(r.Approved, r.ApprovedAt, r.Live, r.CheckedAt, r.Changed, r.ChangedPaths, asOf, packs)
            : new GetProfileResponse(Approved: null, ApprovedAt: null, Live: null, CheckedAt: null, Changed: false, ChangedPaths: [], asOf, packs);
    }

    // The default pack of each loop, then every pack a repo entry names: one row per kind and id, in that order.
    private async ValueTask<IReadOnlyList<ProfilePack>> ResolvePacks(CancellationToken cancellationToken)
    {
        var loaded = await approved.Latest(cancellationToken).ConfigureAwait(false);
        var lenses = loaded.IsError ? null : loaded.Value?.Profile.Workflow?.Packs?.Lenses;
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
