using Aw.Context.Core.Audience;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.ReadLibrary;

/// <summary>The design-doc library's reads (PR review page spec 2026-11-04 §5.5): <see cref="ILibraryStore"/> rows, the caller's audience applied to each, claims resolved when read.</summary>
internal sealed class ContextLibrary(ILibraryStore store) : IContextLibrary
{
    public async ValueTask<Result<IReadOnlyList<LibraryDoc>>> ListDocs(LibraryDocFilter filter, AudienceScope audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(audience);
        var docs = await new LibraryReader(store, audience).Docs(slug: null, cancellationToken).ConfigureAwait(false);
        return docs.IsError ? docs.Error : Results.Success<IReadOnlyList<LibraryDoc>>([.. docs.Value.Where(d => Matches(d, filter))]);
    }

    public ValueTask<Result<LibraryDoc?>> GetDoc(RecordKey key, AudienceScope audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audience);
        return new LibraryReader(store, audience).Doc(key, cancellationToken);
    }

    public async ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audience);
        var reader = new LibraryReader(store, audience);
        var doc = await reader.Doc(docKey, cancellationToken).ConfigureAwait(false);
        return doc.IsError ? doc.Error : doc.Value is null ? Results.Success<IReadOnlyList<LibraryItem>>([]) : await reader.Items(docKey, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<IReadOnlyList<LibraryRefResolution>>> Resolve(IReadOnlyList<string> refs, string? repo, AudienceScope audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentNullException.ThrowIfNull(audience);
        var reader = new LibraryReader(store, audience);
        var resolved = new List<LibraryRefResolution>(refs.Count);
        foreach (var reference in refs)
        {
            var one = await reader.Resolve(reference, repo, cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            resolved.Add(one.Value);
        }

        return resolved;
    }

    public async ValueTask<Result<IReadOnlyList<LibraryClaim>>> Claims(RecordKey prKey, AudienceScope audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audience);
        var rows = await store.ClaimsOf(prKey, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : await Resolved(rows.Value, audience, onlyDoc: null, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<IReadOnlyList<LibraryClaim>>> ClaimsOn(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audience);
        var doc = await new LibraryReader(store, audience).Doc(docKey, cancellationToken).ConfigureAwait(false);
        if (doc.IsError || doc.Value?.Slug is not { } slug)
        {
            return doc.IsError ? doc.Error : Results.Success<IReadOnlyList<LibraryClaim>>([]);
        }

        var rows = await store.ClaimsNaming(slug, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : await Resolved(rows.Value, audience, docKey, cancellationToken).ConfigureAwait(false);
    }

    // A claim is read through its PR's audience and its doc's: the PR must be readable, and the doc is resolved among the readable ones, so an owner-only half is missing, not an error.
    // With 'onlyDoc', only the claims that name that doc, from PRs that were not closed unmerged (spec §4.4: such a claim isn't shown on the doc map).
    private async ValueTask<Result<IReadOnlyList<LibraryClaim>>> Resolved(IReadOnlyList<LibraryClaimRow> rows, AudienceScope audience, RecordKey? onlyDoc, CancellationToken cancellationToken)
    {
        var claims = new List<LibraryClaim>(rows.Count);
        var readable = rows.Where(row => audience.Allows(row.PrAudience, row.Pr) && (onlyDoc is null || row.PrState is not (RecordState.Closed or RecordState.Canceled))).ToList();
        if (readable.Count == 0)
        {
            return claims;
        }

        var reader = new LibraryReader(store, audience);
        foreach (var row in readable)
        {
            var one = await reader.Resolve(row.Ref, LibraryReader.RepoOf(row.Pr), cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            if (onlyDoc is null || (one.Value.Status == LibraryRefStatus.Resolved && one.Value.Doc == onlyDoc))
            {
                claims.Add(new LibraryClaim(row.Pr, row.PrState, one.Value));
            }
        }

        return claims;
    }

    private static bool Matches(LibraryDoc doc, LibraryDocFilter filter) =>
        (filter.Root is null || string.Equals(doc.Root, filter.Root, StringComparison.Ordinal))
        && (filter.Project is null || string.Equals(doc.Project, filter.Project, StringComparison.OrdinalIgnoreCase))
        && (filter.Repo is null || NamesRepo(doc, filter.Repo))
        && (!filter.ErrorsOnly || doc.Errors.Count > 0);

    private static bool NamesRepo(LibraryDoc doc, string repo) =>
        doc.Repos.Any(r => string.Equals(r, repo, StringComparison.OrdinalIgnoreCase))
        || string.Equals(doc.Scope, "repo:" + repo[(repo.IndexOf('/', StringComparison.Ordinal) + 1)..], StringComparison.OrdinalIgnoreCase);
}
