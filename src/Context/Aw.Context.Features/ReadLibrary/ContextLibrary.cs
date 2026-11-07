using Aw.Context.Core;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.ReadLibrary;

/// <summary>The design-doc library's reads (PR review page spec 2026-11-04 §5.5): <see cref="ILibraryStore"/> rows, the caller's audience applied to each, claims resolved when read.</summary>
internal sealed class ContextLibrary(ILibraryStore store, ILibraryStatus? status = null) : IContextLibrary
{
    public ValueTask<Result<IReadOnlyList<LibraryRootStatus>>> Roots(ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Caller.Kind == ContextCallerKind.Session)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<LibraryRootStatus>>>(ContextErrors.SessionsDisabled.With("where the library's roots stand is the owner's, not an agent session's"));
        }

        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<LibraryRootStatus>>>(settings.Error);
        }

        // why: a profile that names no 'context.sources.library' still reads the 'aw' root, as the record feed catalog does
        var library = settings.Value.Library ?? new LibrarySettings();
        var reports = (status?.Read() ?? []).ToDictionary(static r => r.Root, StringComparer.Ordinal);
        var roots = new List<LibraryRootStatus> { library.AwRootOn ? RootStatus("aw", reports) : new LibraryRootStatus("aw", LibraryRootState.Off, Reason: null, Docs: 0, NearMisses: 0, LastRead: null, Path: null, NearMissFindings: []) };
        roots.AddRange(library.Vaults.Select(v => RootStatus(v.Name, reports)));
        return ValueTask.FromResult(Results.Success<IReadOnlyList<LibraryRootStatus>>(roots));
    }

    private static LibraryRootStatus RootStatus(string root, Dictionary<string, LibraryRootReport> reports) =>
        !reports.TryGetValue(root, out var report)
            ? new LibraryRootStatus(root, LibraryRootState.Reading, Reason: null, Docs: 0, NearMisses: 0, LastRead: null, Path: null, NearMissFindings: [])
            : report.Problem is { } problem
                ? new LibraryRootStatus(root, LibraryRootState.Unreadable, problem, Docs: 0, NearMisses: 0, report.LastRead, report.Path, NearMissFindings: [])
                : new LibraryRootStatus(root, LibraryRootState.Ok, Reason: null, report.Docs, report.NearMisses.Count, report.LastRead, report.Path, report.NearMisses);

    public async ValueTask<Result<IReadOnlyList<LibraryDoc>>> ListDocs(LibraryDocFilter filter, AudienceScope audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(audience);
        var docs = await new LibraryReader(store, audience).Docs(slug: null, cancellationToken).ConfigureAwait(false);
        return docs.IsError ? docs.Error : Results.Success<IReadOnlyList<LibraryDoc>>([.. docs.Value.Where(d => Matches(d, filter))]);
    }

    public async ValueTask<Result<IReadOnlyDictionary<string, IReadOnlyList<LibraryDoc>>>> DocsNaming(IReadOnlyList<string> issueKeys, AudienceScope audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issueKeys);
        ArgumentNullException.ThrowIfNull(audience);
        if (issueKeys.Count > IContextLibrary.DocsNamingMaxKeys)
        {
            return ContextErrors.QueryInvalid.With($"{issueKeys.Count} issue keys asked; one call takes at most {IContextLibrary.DocsNamingMaxKeys}");
        }

        var asked = issueKeys.Select(static k => k.ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
        var named = new Dictionary<string, IReadOnlyList<LibraryDoc>>(StringComparer.Ordinal);
        if (asked.Count == 0)
        {
            return named;
        }

        // why: the 200-key cap makes a filter after the audience as cheap as a SQL one, and it keeps the shared-doc-id errors the reader decides
        var docs = await new LibraryReader(store, audience).Docs(slug: null, cancellationToken).ConfigureAwait(false);
        if (docs.IsError)
        {
            return docs.Error;
        }

        var newestFirst = docs.Value.OrderByDescending(static d => d.UpdatedAt ?? DateTimeOffset.MinValue).ThenBy(static d => d.Key.Value, StringComparer.Ordinal).ToList();
        foreach (var key in asked)
        {
            var naming = newestFirst.Where(d => d.Issues.Any(i => string.Equals(i, key, StringComparison.OrdinalIgnoreCase))).ToList();
            if (naming.Count > 0)
            {
                named[key] = naming;
            }
        }

        return named;
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
        return rows.IsError ? rows.Error : await Resolved(rows.Value, new LibraryReader(store, audience), audience, onlyDoc: null, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<IReadOnlyDictionary<RecordKey, IReadOnlyList<LibraryClaim>>>> Claims(IReadOnlyList<RecordKey> prKeys, AudienceScope audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prKeys);
        ArgumentNullException.ThrowIfNull(audience);
        var map = new Dictionary<RecordKey, IReadOnlyList<LibraryClaim>>();
        if (prKeys.Count == 0)
        {
            return map;
        }

        var rows = await store.ClaimsOfMany(prKeys, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        // why: one reader for the whole batch, so a doc two PRs claim is read once.
        var reader = new LibraryReader(store, audience);

        // why: a PR's audience comes with its claim rows, so a PR with claims the caller may not read is left out, while one with no claims (nothing to hide) maps to an empty list.
        foreach (var group in rows.Value.GroupBy(static row => row.Pr))
        {
            var claims = await Resolved([.. group], reader, audience, onlyDoc: null, cancellationToken).ConfigureAwait(false);
            if (claims.IsError)
            {
                return claims.Error;
            }

            if (group.Any(row => audience.Allows(row.PrAudience, row.Pr)))
            {
                map[group.Key] = claims.Value;
            }
        }

        var withRows = rows.Value.Select(static row => row.Pr).ToHashSet();
        foreach (var key in prKeys.Where(k => !withRows.Contains(k)))
        {
            _ = map.TryAdd(key, []);
        }

        return map;
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
        return rows.IsError ? rows.Error : await Resolved(rows.Value, new LibraryReader(store, audience), audience, docKey, cancellationToken).ConfigureAwait(false);
    }

    // A claim is read through its PR's audience and its doc's: the PR must be readable, and the doc is resolved among the readable ones, so an owner-only half is missing, not an error.
    // With 'onlyDoc', only the claims that name that doc, from PRs that were not closed unmerged (spec §4.4: such a claim isn't shown on the doc map).
    private static async ValueTask<Result<IReadOnlyList<LibraryClaim>>> Resolved(IReadOnlyList<LibraryClaimRow> rows, LibraryReader reader, AudienceScope audience, RecordKey? onlyDoc, CancellationToken cancellationToken)
    {
        var claims = new List<LibraryClaim>(rows.Count);
        var readable = rows.Where(row => audience.Allows(row.PrAudience, row.Pr) && (onlyDoc is null || row.PrState is not (RecordState.Closed or RecordState.Canceled))).ToList();
        if (readable.Count == 0)
        {
            return claims;
        }

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
