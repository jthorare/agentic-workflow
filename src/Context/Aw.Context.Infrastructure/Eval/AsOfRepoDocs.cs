using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>
/// The repo's docs as they were at one commit, for the backtest's 'docs' and 'all' arms (S-7A): the same globs, size cap, chunker and ingest
/// scrub as the live index, read from the commit's blobs and held in memory for the run (no database, nothing written). Search is lexical: a chunk
/// scores by how many distinct terms it holds. The live repo-docs source is not touched and keeps SupportsAsOf false.
/// </summary>
internal sealed partial class AsOfRepoDocs : IContextSource
{
    private static readonly SourceDescriptor _descriptor = new(RepoDocs.Id, SourceKind.Doc, SourceFreshness.Snapshot, SupportsAsOf: true);
    private readonly Dictionary<string, Doc> _docs;
    private readonly string _repo;
    private readonly Core.Engine.ContextRepoName _name;
    private readonly TimeProvider _clock;

    private AsOfRepoDocs(Core.Engine.ContextRepoName name, Dictionary<string, Doc> docs, TimeProvider clock)
    {
        _name = name;
        _repo = name.Value;
        _docs = docs;
        _clock = clock;
    }

    /// <summary>Reads and cleans the commit's docs.</summary>
    /// <param name="request">The repo, commit, globs and scrubbers.</param>
    /// <param name="git">Git.</param>
    /// <param name="clock">The clock the hits are stamped with.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The source, or SND-CONTEXT-048 when git can't read the commit.</returns>
    public static async ValueTask<Result<AsOfRepoDocs>> Build(AsOfDocsRequest request, GitCli git, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tree = await git.Tree(request.Repo.Path, request.Commit, cancellationToken).ConfigureAwait(false);
        if (tree.IsError)
        {
            return ContextErrors.ExtractionUnreadable.With($"git can't read {request.Commit[..Math.Min(12, request.Commit.Length)]} of {request.Repo.Name.Value} (fix: check the checkout and that the commit exists)");
        }

        var glob = new Glob(request.Globs);
        var docs = new Dictionary<string, Doc>(StringComparer.Ordinal);
        foreach (var (path, blob) in tree.Value.Where(e => glob.Matches(e.Key) && !request.Repo.Denies(e.Key)).OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (await Prepare(request, git, (path, blob), cancellationToken).ConfigureAwait(false) is { } doc)
            {
                docs[path] = doc;
            }
        }

        return new AsOfRepoDocs(request.Repo.Name, docs, clock);
    }

    public SourceDescriptor Describe() => _descriptor;

    public ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var terms = query.Terms.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var now = _clock.GetUtcNow();
        var scored = _docs.Values
            .SelectMany(d => d.Chunks.Select(c => (Doc: d, Chunk: c, Score: terms.Count(t => c.Text.Contains(t, StringComparison.OrdinalIgnoreCase)))))
            .Where(s => s.Score > 0)
            .OrderByDescending(s => s.Score).ThenBy(s => s.Doc.Path, StringComparer.Ordinal).ThenBy(s => s.Chunk.LineStart)
            .Take(Math.Max(query.Limit, 1))
            .Select((s, i) => Hit(s.Doc, s.Chunk, i + 1, now))
            .OfType<SourceHit>()
            .ToList();
        return ValueTask.FromResult(Results.Success<IReadOnlyList<SourceHit>>(scored));
    }

    public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (Locate(reference) is not { } at)
        {
            return ValueTask.FromResult<Result<SourceRecord>>(ContextErrors.UnknownAnchor.With("not a record of this source"));
        }

        var lines = at.Doc.Clean.Split('\n');
        var start = Math.Clamp(at.Start, 1, lines.Length);
        var text = string.Join('\n', lines[(start - 1)..Math.Clamp(at.End, start, lines.Length)]);
        return ValueTask.FromResult(Results.Success(new SourceRecord(reference, RecordKind.Doc, at.Doc.Title, text, Author: null, CreatedAt: null, UpdatedAt: null)));
    }

    public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refs);
        IReadOnlyList<SourceVersion?> versions = [.. refs.Select(r => Locate(r) is { } at ? SourceVersion.From(at.Doc.Blob) : (SourceVersion?)null)];
        return ValueTask.FromResult(Results.Success(versions));
    }

    public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new SourceHealth(SourceState.Ok, string.Create(CultureInfo.InvariantCulture, $"{_docs.Count} docs held in memory"), Code: null));

    private static async ValueTask<Doc?> Prepare(AsOfDocsRequest request, GitCli git, (string Path, string Blob) entry, CancellationToken cancellationToken)
    {
        if (!RepoDocs.TryChunkId(request.Repo.Name, entry.Path, DocChunker.MaxLine, DocChunker.MaxLine, out _) || !RepoDocs.TryKey(request.Repo.Name, entry.Path, out _))
        {
            return null;   // a path the ids can't carry is never indexed
        }

        var text = await git.Blob(request.Repo.Path, entry.Blob, cancellationToken).ConfigureAwait(false);
        if (text.IsError)
        {
            return null;
        }

        var ingest = request.Scrubbers.Ingest;
        var clean = ingest.Clean(text.Value, singleLine: false);
        if (clean.Blocked)
        {
            return null;
        }

        var title = ingest.Clean(DocChunker.Title(clean.Text, entry.Path), singleLine: true);
        var chunks = DocChunker.Split(clean.Text);
        return new Doc(entry.Path, entry.Blob, title.Blocked ? entry.Path : UntrustedFence.Cap(title.Text), clean.Text.Replace("\r\n", "\n", StringComparison.Ordinal), chunks);
    }

    private SourceHit? Hit(Doc doc, DocChunk chunk, int rank, DateTimeOffset now) =>
        RepoDocs.TryChunkId(_name, doc.Path, chunk.LineStart, chunk.LineEnd, out var id) && RepoDocs.TryKey(_name, doc.Path, out var key) && SourceVersion.TryFrom(doc.Blob, out var version)
            ? new SourceHit(new SourceRef(RepoDocs.Id, id, version, Permalink: null, key), doc.Title, rank, now, Leaky: false)
            : null;

    private (Doc Doc, int Start, int End)? Locate(SourceRef reference)
    {
        var match = ChunkId.Match(reference.Id.Value);
        return match.Success && string.Equals(match.Groups["repo"].Value, _repo, StringComparison.Ordinal) && _docs.TryGetValue(match.Groups["path"].Value, out var doc)
            ? (doc, int.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture))
            : null;
    }

    [GeneratedRegex("^(?<repo>[^/]+)/(?<path>.+)#L(?<a>[0-9]{1,7})-L(?<b>[0-9]{1,7})\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ChunkId { get; }

    private sealed record Doc(string Path, string Blob, string Title, string Clean, IReadOnlyList<DocChunk> Chunks);
}
