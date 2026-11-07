using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Sources;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>
/// The library's view of one PR for the page (spec §4.4, §6.2): its claims and what they resolve to now, the claimed doc's items with who else claims them, the test rows that verify a claimed item, the project's docs
/// for a PR that names none, and how many claimed docs a session may not read. A failed library read leaves the page with its other panels; the docMap and testPlan panels carry the reason.
/// </summary>
/// <param name="engine">Context's facade.</param>
/// <param name="host">The machine's home folders, to write a path with <c language="text">~</c>.</param>
internal sealed class ReviewPageLibraryReader(IContextEngine engine, LibraryHost host)
{
    internal async ValueTask<ReviewPageLibraryFacts> Read(ReviewPageLibraryAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        var folders = LibraryFolders.Of(ask.Settings.Library, host);
        if (ask.Record is not { } record)
        {
            return await ProjectDocs(folders, ask, [], hidden: 0, cancellationToken).ConfigureAwait(false);
        }

        var claims = await engine.Claims(record.Key, ask.Audience, cancellationToken).ConfigureAwait(false);
        if (claims.IsError)
        {
            return Failed(claims.Error.Message, [], hidden: 0);
        }

        var hidden = await Hidden(ask, record.Key, claims.Value.Count, cancellationToken).ConfigureAwait(false);
        var views = claims.Value.Select(c => ReviewPageDocs.Claim(c, record.State)).ToList();
        var first = claims.Value.FirstOrDefault(static c => c.Resolution.Status == LibraryRefStatus.Resolved && c.Resolution.Doc is not null);
        if (first?.Resolution.Doc is not { } docKey)
        {
            return views.Count == 0 ? await ProjectDocs(folders, ask, views, hidden, cancellationToken).ConfigureAwait(false) : new ReviewPageLibraryFacts(Error: null, views, Doc: null, [], [], hidden);
        }

        var claimed = claims.Value.Where(c => c.Resolution.Doc == docKey && c.Resolution.Item is not null).Select(static c => c.Resolution.Item?.Key ?? string.Empty).ToHashSet(StringComparer.Ordinal);
        return await Doc(new DocRead(docKey, record.Key, views, hidden), claimed, ask, folders, cancellationToken).ConfigureAwait(false);
    }

    private sealed record DocRead(RecordKey Doc, RecordKey Pr, IReadOnlyList<ReviewPageClaim> Views, int Hidden);

    private static ReviewPageLibraryFacts Failed(string error, IReadOnlyList<ReviewPageClaim> claims, int hidden) => new(error, claims, Doc: null, [], [], hidden);

    private async ValueTask<ReviewPageLibraryFacts> Doc(DocRead read, HashSet<string> claimed, ReviewPageLibraryAsk ask, LibraryFolders folders, CancellationToken cancellationToken)
    {
        var doc = await engine.GetDoc(read.Doc, ask.Audience, cancellationToken).ConfigureAwait(false);
        var items = await engine.Items(read.Doc, ask.Audience, cancellationToken).ConfigureAwait(false);
        var others = await engine.ClaimsOn(read.Doc, ask.Audience, cancellationToken).ConfigureAwait(false);
        if (doc.IsError || items.IsError || doc.Value is not { } found)
        {
            return Failed(doc.IsError ? doc.Error.Message : items.IsError ? items.Error.Message : "the claimed doc can't be read", read.Views, read.Hidden);
        }

        var view = ReviewPageDocs.Doc(found, items.Value, new ReviewPageDocClaims(claimed, others.IsError ? [] : others.Value, read.Pr), folders);
        return new ReviewPageLibraryFacts(Error: null, read.Views, view, [], ReviewPageDocs.TestRows(items.Value, claimed), read.Hidden);
    }

    private async ValueTask<ReviewPageLibraryFacts> ProjectDocs(LibraryFolders folders, ReviewPageLibraryAsk ask, IReadOnlyList<ReviewPageClaim> claims, int hidden, CancellationToken cancellationToken)
    {
        var docs = await engine.ListDocs(new LibraryDocFilter(Repo: ask.Slug), ask.Audience, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ReviewPageProjectDoc> offered = docs.IsError
            ? []
            : [.. docs.Value.OrderBy(static d => d.Key.Value, StringComparer.Ordinal).SelectMany(d => d.Slug is { } slug ? new[] { new ReviewPageProjectDoc(slug, d.Title, folders.File(d.Key.Value)) } : [])];
        return new ReviewPageLibraryFacts(docs.IsError ? docs.Error.Message : null, claims, Doc: null, offered, [], hidden);
    }

    // why: a session's claims leave out the docs it may not read, so the count is what a human would see minus what the session does
    private async ValueTask<int> Hidden(ReviewPageLibraryAsk ask, RecordKey pr, int visible, CancellationToken cancellationToken)
    {
        if (ask.Scope.Caller.Kind != ContextCallerKind.Session)
        {
            return 0;
        }

        var all = await ReviewScopes.HumanAudience(ask.Settings, ask.Scope, cancellationToken).ConfigureAwait(false);
        var claims = all.IsError ? null : await engine.Claims(pr, all.Value, cancellationToken).ConfigureAwait(false);
        return claims is { IsSuccess: true } ? Math.Max(0, claims.Value.Select(static c => c.Resolution.Doc?.Value ?? c.Resolution.Ref).Distinct(StringComparer.Ordinal).Count() - visible) : 0;
    }
}
