using Aw.Context.Core.Audience;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.ReadLibrary;

/// <summary>
/// One library read (PR review page spec 2026-11-04 §4.4, §5.4): the docs the caller may read, who keeps a shared doc id, and what a ref names. It asks the store once per doc id and once per doc's items, and
/// applies <see cref="AudienceScope.Allows(AudienceKey?, RecordKey)"/> to every doc, so a doc the caller may not read is as good as missing and a claim on it is missing too.
/// </summary>
internal sealed class LibraryReader(ILibraryStore store, AudienceScope audience)
{
    private readonly Dictionary<string, IReadOnlyList<LibraryDoc>> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<RecordKey, IReadOnlyList<LibraryItem>> _items = [];
    private IReadOnlyList<LibraryDoc>? _all;

    /// <summary>The readable docs with this id (all docs for null), in key order, each marked when another doc keeps its id.</summary>
    public async ValueTask<Result<IReadOnlyList<LibraryDoc>>> Docs(string? slug, CancellationToken cancellationToken)
    {
        if (slug is null ? _all is { } all : _byId.TryGetValue(slug, out all))
        {
            return Results.Success(all);
        }

        var rows = await store.Docs(new RecordAudience(audience.Allowed, audience.Records), slug, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var readable = Mark([.. rows.Value.Where(d => audience.Allows(d.Audience, d.Key))]);
        if (slug is null)
        {
            _all = readable;
        }
        else
        {
            _byId[slug] = readable;
        }

        return Results.Success<IReadOnlyList<LibraryDoc>>(readable);
    }

    /// <summary>Reads one doc, null when there is none or the caller may not read it.</summary>
    public async ValueTask<Result<LibraryDoc?>> Doc(RecordKey key, CancellationToken cancellationToken)
    {
        var doc = await store.Doc(key, cancellationToken).ConfigureAwait(false);
        if (doc.IsError || doc.Value is not { } found || !audience.Allows(found.Audience, found.Key))
        {
            return doc.IsError ? doc.Error : Results.Success<LibraryDoc?>(value: null);
        }

        if (found.Slug is null)
        {
            return found;
        }

        var siblings = await Docs(found.Slug, cancellationToken).ConfigureAwait(false);
        return siblings.IsError ? siblings.Error : Results.Success<LibraryDoc?>(siblings.Value.FirstOrDefault(d => d.Key == found.Key) ?? found);
    }

    /// <summary>A readable doc's items in document order.</summary>
    public async ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey doc, CancellationToken cancellationToken)
    {
        if (_items.TryGetValue(doc, out var cached))
        {
            return Results.Success(cached);
        }

        var rows = await store.Items(doc, cancellationToken).ConfigureAwait(false);
        if (!rows.IsError)
        {
            _items[doc] = rows.Value;
        }

        return rows;
    }

    /// <summary>What one ref names among the docs the caller may read.</summary>
    /// <param name="reference">The ref as written: <c language="text">doc:&lt;scope&gt;/&lt;doc id&gt;#&lt;item&gt;</c> or <c language="text">&lt;doc id&gt;#&lt;item&gt;</c>.</param>
    /// <param name="repo">The PR's repo ('owner/name') for a short ref.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The resolution.</returns>
    public async ValueTask<Result<LibraryRefResolution>> Resolve(string reference, string? repo, CancellationToken cancellationToken)
    {
        var missing = new LibraryRefResolution(reference, LibraryRefStatus.Missing, Doc: null, Item: null);
        if (Split(reference) is not (var scope, var slug, var item))
        {
            return missing;
        }

        var docs = await Docs(slug, cancellationToken).ConfigureAwait(false);
        if (docs.IsError)
        {
            return docs.Error;
        }

        var holders = docs.Value.Where(Holds).ToList();
        var (chosen, ambiguous) = scope is null ? Short(holders, repo) : (holders.Find(d => string.Equals(d.Scope, scope, StringComparison.OrdinalIgnoreCase)), false);
        if (chosen is null)
        {
            return ambiguous ? missing with { Status = LibraryRefStatus.Ambiguous } : missing;
        }

        var items = await Items(chosen.Key, cancellationToken).ConfigureAwait(false);
        var found = items.IsError ? null : items.Value.FirstOrDefault(i => string.Equals(i.Key, item, StringComparison.Ordinal));
        return items.IsError ? items.Error : found is null ? missing : new LibraryRefResolution(reference, LibraryRefStatus.Resolved, chosen.Key, found);
    }

    /// <summary>A PR's repo ('owner/name') from its record key ('github:pr/owner/name/number').</summary>
    public static string? RepoOf(RecordKey pr)
    {
        const string Prefix = "github:pr/";
        var key = pr.Value;
        var end = key.LastIndexOf('/');
        return key.StartsWith(Prefix, StringComparison.Ordinal) && end > Prefix.Length ? key[Prefix.Length..end] : null;
    }

    // 'doc:<scope>/<doc id>#<item>' or '<doc id>#<item>': the scope is null for a short ref; null for a ref that has no doc or no item.
    private static (string? Scope, string Slug, string Item)? Split(string reference)
    {
        var hash = reference.LastIndexOf('#');
        if (hash <= 0 || hash == reference.Length - 1)
        {
            return null;
        }

        var head = reference[..hash];
        var item = NoLeadingZeros(reference[(hash + 1)..]);
        if (!head.StartsWith("doc:", StringComparison.Ordinal))
        {
            return (null, head, item);
        }

        var slash = head.LastIndexOf('/');
        return slash <= "doc:".Length ? null : (head["doc:".Length..slash], head[(slash + 1)..], item);
    }

    // A short ref looks in the PR repo's own root first, then in the aw and vault docs whose 'repos:' name that repo; two docs at one step is ambiguous (SND-LIB-022).
    private static (LibraryDoc? Doc, bool Ambiguous) Short(List<LibraryDoc> holders, string? repo)
    {
        if (repo is null)
        {
            return (null, false);
        }

        var name = repo[(repo.IndexOf('/', StringComparison.Ordinal) + 1)..];
        if (holders.Find(d => string.Equals(d.Scope, "repo:" + name, StringComparison.OrdinalIgnoreCase)) is { } own)
        {
            return (own, false);
        }

        var named = holders.Where(d => !d.Scope.StartsWith("repo:", StringComparison.Ordinal) && d.Repos.Any(r => string.Equals(r, repo, StringComparison.OrdinalIgnoreCase))).ToList();
        return named.Count == 1 ? (named[0], false) : (null, named.Count > 1);
    }

    // The doc that keeps its id: it has one, its front matter reads, and no other doc of its scope beat it to it.
    private static bool Holds(LibraryDoc doc) =>
        DesignDocFrontMatter.IsDocId(doc.Slug) && !doc.Errors.Any(static e => string.Equals(e.Code, DesignDocCodes.SharedDocId, StringComparison.Ordinal));

    // The live doc at the lowest key keeps a shared id and the others show SND-LIB-012; a doc removed later hands the id to the next one on the next read.
    private static List<LibraryDoc> Mark(List<LibraryDoc> docs)
    {
        var losers = new Dictionary<RecordKey, DesignDocError>();
        foreach (var group in docs.Where(static d => DesignDocFrontMatter.IsDocId(d.Slug)).GroupBy(static d => (d.Scope, d.Slug)))
        {
            var ordered = group.OrderBy(static d => d.Key.Value, StringComparer.Ordinal).ToList();
            foreach (var loser in ordered.Skip(1))
            {
                losers[loser.Key] = new DesignDocError(DesignDocCodes.SharedDocId, Line: null, $"doc id \"{group.Key.Slug}\" is also used by {ordered[0].Key.Value}.", "Rename one of them.");
            }
        }

        return [.. docs.Select(d => losers.TryGetValue(d.Key, out var error) ? d with { Errors = [.. d.Errors, error] } : d)];
    }

    // A phase is stored without leading zeros, as the parser keeps it: 'Ph01' names 'Ph1' and 'Ph2-01' names 'Ph2-1'.
    private static string NoLeadingZeros(string item)
    {
        if (!item.StartsWith("Ph", StringComparison.Ordinal))
        {
            return item;
        }

        var end = 2;
        while (end < item.Length && char.IsAsciiDigit(item[end]))
        {
            end++;
        }

        var number = Trimmed(item[2..end]);
        var rest = item[end..];
        return rest.Length > 1 && rest[0] == '-' && rest[1..].All(char.IsAsciiDigit)
            ? "Ph" + number + "-" + Trimmed(rest[1..])
            : "Ph" + number + rest;
    }

    private static string Trimmed(string digits)
    {
        if (digits.Length == 0)
        {
            return digits;
        }

        var trimmed = digits.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }
}
