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

        var (pages, version, profile, map) = read.Value;
        var rows = PageListPreview.Of(PageListText.Parse(request.Text), pages, Concepts(profile, map));
        return new PagesPreview(map.Version ?? version, [.. rows.Select(r => Block(r, request.Repo))]);
    }

    // The repo, the caller's scope, every page it may read, the live profile and the map's facts; the first refusal stops.
    private async ValueTask<Result<(List<PageRow> Pages, string Version, SindriProfile Profile, MapPageFacts Map)>> Read(string? repoText, CancellationToken cancellationToken)
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

        return (pages.Value.Rows, pages.Value.MapVersion, profile.Value.Profile, map.Value);
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

    // The pins first, in profile order (the top-level list, then each repo's), then the map's concepts; one whose id can't be a concept id is left out.
    private static List<(ConceptId Id, string Name)> Concepts(SindriProfile profile, MapPageFacts map)
    {
        var pins = (profile.Index?.Concepts ?? []).Concat((profile.Repos ?? []).SelectMany(static r => r.Index?.Concepts ?? []))
            .Select(static p => (Id: p.Id?.Value ?? PinSlug.Of(p.Name ?? string.Empty), Name: p.Name ?? string.Empty));
        return [.. pins.Concat(map.Concepts.Select(static c => (Id: c.Key, c.Name)))
            .Where(static c => ConceptId.TryFrom(c.Id, out _)).Select(static c => (ConceptId.From(c.Id), c.Name))];
    }

    private static PagesPreviewBlock Block(PageListRow row, string? repo) =>
        new(row.Block, row.Line, row.Name, row.Match, row.Replaces, row.SameAs, row.Ticked, row.Reason, row.Actions, row.UnknownConcepts, row.Page is { } page ? Settings(page, repo) : null);

    // The page as POST /sindri/context/pages takes it. The id is left to the name's slug unless the page matched a suggested one with another id: then the save must use that id, or 'replaces' and 'same as' would not hold.
    private static PageSettings Settings(PagePin page, string? repo)
    {
        var slug = PageSlugs.Of(page.Name);
        return new PageSettings(
            string.Equals(page.Id.Value, slug, StringComparison.Ordinal) ? null : PagePinId.From(page.Id.Value),
            page.Name,
            page.Repo?.Value ?? repo,
            page.SurfaceKey,
            [.. page.Actions.Select(static a => new PageActionSettings(Id: null, a.Name, a.ActionKey, [.. a.Concepts]))]);
    }
}
