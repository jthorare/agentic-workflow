using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Context.Core;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>
/// The repo-docs source over the repos in a call's scope (spec §5.2): lexical search in the scrubbed FTS5 chunks
/// (each term a quoted phrase, never query syntax; only documents stored under the call's scrub ruleset, so nothing
/// cleaned with other rules is a hit), fetch from git at the cited blob and line span, and versions from one tree listing per repo at HEAD. Its records are chunks
/// ('&lt;repo&gt;/&lt;path&gt;#L&lt;a&gt;-L&lt;b&gt;'); the canonical key is the document, so chunks of one document merge.
/// </summary>
internal sealed partial class RepoDocsSource(IReadOnlyList<ContextRepo> repos, ContextDatabase database, GitCli git, Scrubbers scrubbers, TimeProvider clock)
    : IContextSource
{
    private static readonly SourceDescriptor _descriptor = new(RepoDocs.Id, SourceKind.Doc, SourceFreshness.Snapshot, SupportsAsOf: false) { HonorsPin = true };

    public SourceDescriptor Describe() => _descriptor;

    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions || query.Terms.Count == 0)
        {
            return readable.IsError ? Unavailable(readable.Error) : Array.Empty<SourceHit>();
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return Unavailable(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(read.Value, new SqlStatement(DocSql.Search, c => c
                    .Bind("@match", string.Join(" OR ", query.Terms.Select(DocSql.Phrase)))
                    .Bind("@repos", RepoList())
                    .Bind("@ruleset", scrubbers.Ruleset.Value)
                    .Bind("@limit", Math.Max(query.Limit, 1))), DocMappers.Hit, cancellationToken)
                .ConfigureAwait(false);
            var now = clock.GetUtcNow();
            return rows.IsError
                ? Unavailable(rows.Error)
                : rows.Value.Where(row => InPin(query, row)).SelectMany(row => Ref(row) is { } reference ? [(Row: row, Ref: reference)] : Array.Empty<(HitRow Row, SourceRef Ref)>())
                    .Select((hit, i) => new SourceHit(hit.Ref, hit.Row.Title, i + 1, now, Leaky: false)).ToList();
        }
    }

    public async ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (Parse(reference.Id) is not { } at)
        {
            return ContextErrors.UnknownAnchor.With("not a record of this source");
        }

        var blob = await git.Blob(at.Repo.Path, reference.Version.Value, cancellationToken).ConfigureAwait(false);
        if (blob.IsError)
        {
            return blob.Error;
        }

        var clean = CleanDoc(blob.Value);
        return Cut(reference, at, clean);
    }

    /// <summary>Many chunks in one go (#3085): one 'git cat-file --batch' per repo for the distinct blobs, each blob cleaned once, then each ref's span cut as <see cref="Fetch"/> cuts it.</summary>
    public async ValueTask<Result<IReadOnlyList<Result<SourceRecord>>>> FetchMany(IReadOnlyList<SourceRef> references, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(references);
        var parsed = references.Select(r => (Ref: r, At: Parse(r.Id))).ToList();
        var (blobs, failed) = await ReadBlobs([.. parsed], cancellationToken).ConfigureAwait(false);
        var cleaned = new Dictionary<(string Repo, string Blob), ScrubbedField>();
        var records = new List<Result<SourceRecord>>(parsed.Count);
        foreach (var (reference, at) in parsed)
        {
            if (at is not { } where)
            {
                records.Add(ContextErrors.UnknownAnchor.With("not a record of this source"));
                continue;
            }

            if (failed.TryGetValue(where.Repo.Path, out var error))
            {
                records.Add(error);   // only this repo's refs carry its failure (or the missed deadline); other repos' docs stay
                continue;
            }

            var key = (where.Repo.Path, reference.Version.Value);
            if (blobs.GetValueOrDefault(key) is not { } text)
            {
                records.Add(ContextErrors.UnknownAnchor.With("the repo has no such blob"));   // what Fetch gives for a missing blob
                continue;
            }

            if (!cleaned.TryGetValue(key, out var clean))
            {
                cleaned[key] = clean = CleanDoc(text);
            }

            records.Add(Cut(reference, where, clean));
        }

        return records;
    }

    // One 'cat-file --batch' per repo for the distinct blob ids the refs cite; null text = the repo has no such blob.
    // One batch read per repo. A repo git can't read fails only its own refs; when the deadline fires, the repos read so far stay and
    // the rest are marked MissedDeadline, so the pipeline counts them as unread (#3243 review).
    private async ValueTask<(Dictionary<(string Repo, string Blob), string?> Blobs, Dictionary<string, Error> Failed)> ReadBlobs(List<(SourceRef Ref, (ContextRepo Repo, string Path, int Start, int End)? At)> parsed, CancellationToken cancellationToken)
    {
        var blobs = new Dictionary<(string Repo, string Blob), string?>();
        var failed = new Dictionary<string, Error>(StringComparer.Ordinal);
        foreach (var group in parsed.Where(p => p.At is not null).GroupBy(p => p.At.GetValueOrDefault().Repo.Path, StringComparer.Ordinal))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                failed[group.Key] = ContextErrors.MissedDeadline(RepoDocs.Id.Value);
                continue;
            }

            var ids = group.Select(p => p.Ref.Version.Value).Distinct(StringComparer.Ordinal).ToList();
            Result<IReadOnlyDictionary<string, string?>> read;
            try
            {
                read = await git.Blobs(group.Key, ids, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                failed[group.Key] = ContextErrors.MissedDeadline(RepoDocs.Id.Value);
                continue;
            }

            if (read.IsError)
            {
                failed[group.Key] = read.Error;
                continue;
            }

            foreach (var (id, text) in read.Value)
            {
                blobs[(group.Key, id)] = text;
            }
        }

        return (blobs, failed);
    }

    // The whole document is cleaned before the span is cut (line-preserving), the same way it was indexed, so a
    // secret or comment that crosses the span's edge can't come back half-scrubbed. The engine cleans the slice
    // again at its own boundary, which changes nothing.
    private ScrubbedField CleanDoc(string blob) => scrubbers.Ingest.Clean(blob.Replace("\r\n", "\n", StringComparison.Ordinal), singleLine: false);

    private static SourceRecord Cut(SourceRef reference, (ContextRepo Repo, string Path, int Start, int End) at, ScrubbedField clean)
    {
        var lines = clean.Text.Split('\n');
        var start = Math.Clamp(at.Start, 1, lines.Length);
        var text = clean.Blocked ? clean.Text : string.Join('\n', lines[(start - 1)..Math.Clamp(at.End, start, lines.Length)]);
        return new SourceRecord(reference, RecordKind.Doc, DocChunker.Title(clean.Text, at.Path), clean.Blocked ? string.Empty : text, Author: null, CreatedAt: null,
            UpdatedAt: null)
        { Blocked = clean.Blocked };
    }

    public async ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refs);
        var trees = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        var versions = new List<SourceVersion?>();
        foreach (var reference in refs)
        {
            if (Parse(reference.Id) is not { } at)
            {
                versions.Add(null);
                continue;
            }

            if (!trees.TryGetValue(at.Repo.Name.Value, out var tree))
            {
                var head = await git.Head(at.Repo.Path, cancellationToken).ConfigureAwait(false);
                var listed = head.IsError ? head.Error : await git.Tree(at.Repo.Path, head.Value, cancellationToken).ConfigureAwait(false);
                if (listed.IsError)
                {
                    return listed.Error;
                }

                trees[at.Repo.Name.Value] = tree = listed.Value;
            }

            versions.Add(tree.TryGetValue(at.Path, out var blob) ? SourceVersion.From(blob) : null);
        }

        return versions;
    }

    public async ValueTask<SourceHealth> Probe(CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError)
        {
            return new SourceHealth(SourceState.Unavailable, "the store can't be opened", readable.Error.Code);
        }

        foreach (var repo in repos)
        {
            if ((await git.Head(repo.Path, cancellationToken).ConfigureAwait(false)).IsError)
            {
                return new SourceHealth(SourceState.Unavailable, $"git can't read repo '{repo.Name.Value}'", ContextErrors.SourceUnavailable.Code);
            }
        }

        return new SourceHealth(SourceState.Ok, string.Create(CultureInfo.InvariantCulture, $"{repos.Count} repos readable"), Code: null);
    }

    // Under a pin a doc stays when its record is linked into the pin, or failing a link when its path is inside the pin's code paths; the rest are counted by the pin they sit in.
    private static bool InPin(SourceQuery query, HitRow row) =>
        query.Pin is not { } pin || pin.HoldsDoc(row.Repo, row.Path);

    /// <summary>The hit's reference, or null when its repo or path can't be carried by the ids (never indexed since S-1A's review, but an older row is skipped, never thrown).</summary>
    private static SourceRef? Ref(HitRow row) =>
        ContextRepoName.TryFrom(row.Repo, out var repo)
        && RepoDocs.TryChunkId(repo, row.Path, row.LineStart, row.LineEnd, out var id)
        && SourceVersion.TryFrom(row.Blob, out var version)
        && RepoDocs.TryKey(repo, row.Path, out var key)
            ? new SourceRef(RepoDocs.Id, id, version, Permalink: null, key)
            : null;

    private (ContextRepo Repo, string Path, int Start, int End)? Parse(ExternalId id)
    {
        var match = ChunkId.Match(id.Value);
        var repo = match.Success ? repos.FirstOrDefault(r => string.Equals(r.Name.Value, match.Groups["repo"].Value, StringComparison.Ordinal)) : null;
        return repo is null
            ? null
            : (repo, match.Groups["path"].Value, int.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>The repos in scope as a JSON array of names, for json_each (written by the JSON writer, so any name is escaped correctly).</summary>
    private string RepoList()
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartArray();
            foreach (var repo in repos)
            {
                json.WriteStringValue(repo.Name.Value);
            }

            json.WriteEndArray();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static Error Unavailable(Error error) => ContextErrors.SourceUnavailable.With($"repo-docs can't read its index ({error.Code})");

    [GeneratedRegex("^(?<repo>[^/]+)/(?<path>.+)#L(?<a>[0-9]{1,7})-L(?<b>[0-9]{1,7})\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ChunkId { get; }
}
