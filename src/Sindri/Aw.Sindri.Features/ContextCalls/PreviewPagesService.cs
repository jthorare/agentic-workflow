using System.Globalization;
using System.Text;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Pages;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Reads a pasted page list against the pages the caller may read (through the engine, which gates and scopes sessions), the live profile's pins and the map's concepts, and runs the page rules over each block (<see cref="PageListPreview"/>).
/// Nothing is written or queued. The paste is checked first (SND-CONTEXT-259), then the repo (SND-CONTEXT-258, worded as the pages read words it); no map is whatever the pages read answers.
/// </summary>
internal sealed class PreviewPagesService(ContextScopes scopes, IContextEngine engine, IProfileSource live, IMapPageFacts facts) : IService<PreviewPagesRequest, PagesPreview>
{
    private const int MaxBytes = 65_536;
    private const int PageSize = 200;

    public async ValueTask<Result<PagesPreview>> Handle(PreviewPagesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Check(request.Text) is { } refused)
        {
            return refused;
        }

        var read = await Read(request.Repo, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var (pages, version, profile, hash, map) = read.Value;
        var rows = PageListPreview.Of(PageListText.Parse(request.Text), pages, Concepts(profile, map), OwnerPages(profile));
        return new PagesPreview(map.Version ?? version, hash, [.. rows.Select(r => Block(r, request.Repo, profile))]);
    }

    // The repo, the caller's scope, every page it may read, the live profile and the map's facts; the first refusal stops.
    private async ValueTask<Result<(List<PageRow> Pages, string Version, SindriProfile Profile, ProfileHash Hash, MapPageFacts Map)>> Read(string? repoText, CancellationToken cancellationToken)
    {
        var repo = GetContextPagesService.Repo(repoText);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (repo.IsError || scope.IsError)
        {
            return repo.IsError ? repo.Error : scope.Error;
        }

        var pages = await AllPages(repo.Value, scope.Value, cancellationToken).ConfigureAwait(false);
        var profile = await live.Load(cancellationToken).ConfigureAwait(false);
        var map = await facts.Read(cancellationToken).ConfigureAwait(false);
        if (pages.IsError || profile.IsError || map.IsError)
        {
            return pages.IsError ? pages.Error : profile.IsError ? profile.Error : map.Error;
        }

        return (pages.Value.Rows, pages.Value.MapVersion, profile.Value.Profile, profile.Value.Hash, map.Value);
    }

    private static Error? Check(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ContextErrors.PagePreviewInvalid.With("pages preview: nothing pasted (fix: one page per block: its name, then one action per line)");
        }

        var bytes = Encoding.UTF8.GetByteCount(text);
        return bytes > MaxBytes
            ? ContextErrors.PagePreviewInvalid.With(string.Create(CultureInfo.InvariantCulture, $"pages preview: {bytes:N0} bytes; at most {MaxBytes:N0}"))
            : null;
    }

    // Every page the caller may read, 200 at a time until the engine says no more follow.
    private async ValueTask<Result<(List<PageRow> Rows, string MapVersion)>> AllPages(ContextRepoName? repo, ContextScope scope, CancellationToken cancellationToken)
    {
        var rows = new List<PageRow>();
        while (true)
        {
            var query = new PagesQuery(Concept: null, Page: null, repo, PageMarks.All, PageSize, rows.Count, Draft: false, MapVersion: null);
            var slice = await engine.Pages(query, scope, cancellationToken).ConfigureAwait(false);
            if (slice.IsError)
            {
                return slice.Error;
            }

            rows.AddRange(slice.Value.Pages);
            if (!slice.Value.Truncated || slice.Value.Pages.Count == 0)
            {
                return (rows, slice.Value.MapVersion);
            }
        }
    }

    // Every page the profile names, the top-level list and each repo's own: the 200-page cap counts them all.
    private static int OwnerPages(SindriProfile profile) =>
        (profile.Index?.Pages?.Count ?? 0) + (profile.Repos ?? []).Sum(static r => r.Index?.Pages?.Count ?? 0);

    // The pins first, in profile order (the top-level list, then each repo's), then the map's concepts; one whose id can't be a concept id is left out.
    private static List<(ConceptId Id, string Name)> Concepts(SindriProfile profile, MapPageFacts map)
    {
        var pins = (profile.Index?.Concepts ?? []).Concat((profile.Repos ?? []).SelectMany(static r => r.Index?.Concepts ?? []))
            .Select(static p => (Id: p.Id?.Value ?? PinSlug.Of(p.Name ?? string.Empty), Name: p.Name ?? string.Empty));
        return [.. pins.Concat(map.Concepts.Select(static c => (Id: c.Key, c.Name)))
            .Where(static c => ConceptId.TryFrom(c.Id, out _)).Select(static c => (ConceptId.From(c.Id), c.Name))];
    }

    private static PagesPreviewBlock Block(PageListRow row, string? repo, SindriProfile profile) =>
        new(row.Block, row.Line, row.Name, row.Match, row.Replaces, row.SameAs, row.Ticked, row.Reason, row.Actions, row.UnknownConcepts, row.Page is { } page ? Settings(page, repo, row.Replaces is { } replaced ? OwnerActions(profile, replaced.Value) : []) : null);

    // The actions of the profile page a block replaces (the top-level list, then each repo's), or none.
    private static List<PageActionSettings> OwnerActions(SindriProfile profile, string pageId)
    {
        var pages = (profile.Index?.Pages ?? []).Concat((profile.Repos ?? []).SelectMany(static r => r.Index?.Pages ?? []));
        var owner = pages.FirstOrDefault(p => string.Equals(Effective(p.Id?.Value, p.Name), pageId, StringComparison.Ordinal));
        return [.. owner?.Actions ?? []];
    }

    // The page as POST /sindri/context/pages takes it. The id is left to the name's slug unless the page matched a suggested one with another id: then the save must use that id, or 'replaces' and 'same as' would not hold.
    // An action of a replaced page keeps its id: the first unused existing action with the same map action key, else the same name slug. Every other action gets its name's slug, numbered past the kept ids and each other ('send-it-2'); an id equal to the bare slug is left null.
    private static PageSettings Settings(PagePin page, string? repo, List<PageActionSettings> existing)
    {
        var slug = PageSlugs.Of(page.Name);
        var kept = page.Actions.Select(a => KeptId(a, existing)).ToList();
        var taken = new HashSet<string>(kept.OfType<string>(), StringComparer.Ordinal);
        return new PageSettings(
            string.Equals(page.Id.Value, slug, StringComparison.Ordinal) ? null : PagePinId.From(page.Id.Value),
            page.Name,
            page.Repo?.Value ?? repo,
            page.SurfaceKey,
            [.. page.Actions.Select((a, i) => new PageActionSettings(ActionId(a.Name, kept[i], taken), a.Name, a.ActionKey, [.. a.Concepts]))]);
    }

    private static PageActionPinId? ActionId(string name, string? kept, HashSet<string> taken)
    {
        var slug = PageSlugs.Of(name);
        var id = kept ?? PageSlugs.Unique(slug, taken);
        return string.Equals(id, slug, StringComparison.Ordinal) ? null : PageActionPinId.From(id);
    }

    private static string Effective(string? id, string? name) => id ?? PageSlugs.Of(name ?? string.Empty);

    // The effective id of the existing action this one keeps, or null.
    private static string? KeptId(PageActionPin action, List<PageActionSettings> existing)
    {
        var slug = PageSlugs.Of(action.Name);
        var found = existing.FirstOrDefault(e => action.ActionKey is not null && string.Equals(e.Action, action.ActionKey, StringComparison.Ordinal)) ?? existing.FirstOrDefault(e => string.Equals(Effective(id: null, e.Name), slug, StringComparison.Ordinal));
        if (found is null)
        {
            return null;
        }

        _ = existing.Remove(found);
        return Effective(found.Id?.Value, found.Name);
    }
}
