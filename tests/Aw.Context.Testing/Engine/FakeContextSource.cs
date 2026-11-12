using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The in-memory <see cref="IContextSource"/>: records in memory, ranked by how many query terms their text holds.
/// It can be made unavailable, slow (it waits until the deadline's token fires) or moved on (Versions reports a newer
/// version than the one its hits cite, as a snapshot index behind its repo does). It passes <see cref="ContextSourceContract{TSource}"/>.
/// </summary>
/// <param name="descriptor">What it says it is.</param>
/// <param name="records">What it holds.</param>
public sealed class FakeContextSource(SourceDescriptor descriptor, IReadOnlyList<FakeRecord> records) : IContextSource
{
    private readonly Dictionary<ExternalId, SourceVersion?> _current = records.ToDictionary(r => r.Ref.Id, r => (SourceVersion?)r.Ref.Version);

    /// <summary>When set, every call but Describe fails with this error.</summary>
    public Error? Failure { get; set; }

    /// <summary>True: Search waits until its token is cancelled (a source that misses the deadline).</summary>
    public bool Hangs { get; set; }

    /// <summary>True: Fetch waits until its token is cancelled (a fetch that misses the deadline).</summary>
    public bool FetchHangs { get; set; }

    /// <summary>When set, Fetch (only) fails with this error; FetchMany returns it as every entry's result, not as a failed call.</summary>
    public Error? FetchFailure { get; set; }

    /// <summary>True: Fetch reports the record as already withheld by a whole-document scrub (SourceRecord.Blocked).</summary>
    public bool FetchBlocked { get; set; }

    /// <summary>True: Versions fails (the stale check can't run).</summary>
    public bool VersionsFail { get; set; }

    /// <summary>Runs at the start of every Fetch: a test advances its clock here to give each fetch a cost (a git read per record, #3085).</summary>
    public Action? OnFetch { get; set; }

    /// <summary>Runs at the start of every FetchMany call: a test advances its clock here to give one batched read a cost (#3085).</summary>
    public Action? OnFetchMany { get; set; }

    /// <summary>How many records were read, one at a time or in a batch (a record read twice counts twice).</summary>
    public int DocReads { get; private set; }

    /// <summary>How many records were read inside a FetchMany call.</summary>
    public int BatchedReads { get; private set; }

    /// <summary>How many Fetch and FetchMany calls it answered: the round trips to the source.</summary>
    public int ReadCalls { get; private set; }

    /// <summary>The searches it answered.</summary>
    public IList<SourceQuery> Searches { get; } = [];

    /// <summary>The repo-docs fake over the split-panes fixture's two docs, as repo 'split-panes'.</summary>
    /// <returns>The source.</returns>
    public static FakeContextSource FixtureDocs() => new(new SourceDescriptor(RepoDocs.Id, SourceKind.Doc, SourceFreshness.Snapshot, SupportsAsOf: false),
    [
        Doc("docs/architecture.md", "Architecture", SplitPanesFixture.ReadText("app/00-initial/docs/architecture.md"), "a1"),
        Doc("docs/decisions/0001-orientation-toggle.md", "0001 Orientation toggle", SplitPanesFixture.ReadText("app/03-spl-103/docs/decisions/0001-orientation-toggle.md"), "b2"),
    ]);

    /// <summary>A repo-docs record of repo 'split-panes'.</summary>
    /// <param name="path">Its repo-relative path.</param>
    /// <param name="title">Its title.</param>
    /// <param name="text">Its text.</param>
    /// <param name="version">Its version.</param>
    /// <returns>The record.</returns>
    public static FakeRecord Doc(string path, string title, string text, string version) =>
        new(new SourceRef(RepoDocs.Id, ExternalId.From("split-panes/" + path), SourceVersion.From(version), Permalink: null, RepoDocs.Key(ContextRepoName.From("split-panes"), path)), title, text);

    /// <summary>One chunk of a repo-docs document: its own id (a line span of the path), the document's canonical key, as RepoDocs cites chunks.</summary>
    /// <param name="path">The document's repo-relative path.</param>
    /// <param name="chunk">The chunk's number; its span is lines chunk..chunk.</param>
    /// <param name="text">Its text.</param>
    /// <param name="version">Its version (the document's blob).</param>
    /// <returns>The record.</returns>
    public static FakeRecord Chunk(string path, int chunk, string text, string version) =>
        new(new SourceRef(RepoDocs.Id, ExternalId.From(string.Create(CultureInfo.InvariantCulture, $"split-panes/{path}#L{chunk}-L{chunk}")), SourceVersion.From(version), Permalink: null,
            RepoDocs.Key(ContextRepoName.From("split-panes"), path)), "Doc " + path, text);

    /// <summary>Makes <paramref name="id"/>'s current version <paramref name="version"/> (null: gone).</summary>
    /// <param name="id">The record.</param>
    /// <param name="version">Its new version.</param>
    public void MoveOn(ExternalId id, SourceVersion? version) => _current[id] = version;

    /// <inheritdoc />
    public SourceDescriptor Describe() => descriptor;

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Searches.Add(query);
        if (Hangs)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        if (Failure is { } failure)
        {
            return failure;
        }

        var hits = records
            .Select(r => (r, Score: query.Terms.Count(t => r.Text.Contains(t, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.r.Ref.Id.Value, StringComparer.Ordinal)
            .Take(query.Limit)
            .Select((x, i) => new SourceHit(x.r.Ref, x.r.Title, i + 1, DateTimeOffset.UnixEpoch, Leaky: false) { Aliases = x.r.Aliases });
        return hits.ToList();
    }

    /// <inheritdoc />
    public async ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ReadCalls++;
        OnFetch?.Invoke();
        if (FetchHangs)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        return (FetchFailure ?? Failure) is { } failure ? failure : Read(reference);
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<Result<SourceRecord>>>> FetchMany(IReadOnlyList<SourceRef> references, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(references);
        ReadCalls++;
        OnFetchMany?.Invoke();
        if (FetchHangs)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        if (Failure is { } failure)
        {
            return failure;   // the whole source is down: the call fails
        }

        BatchedReads += references.Count;
        List<Result<SourceRecord>> answers = FetchFailure is { } each
            ? [.. references.Select(_ => (Result<SourceRecord>)each)]   // a record-level failure (gone) is each entry's, as with Fetch
            : [.. references.Select(Read)];
        return answers;
    }

    private Result<SourceRecord> Read(SourceRef reference)
    {
        DocReads++;
        var found = records.FirstOrDefault(r => r.Ref == reference);
        return found is null
            ? ContextErrors.UnknownAnchor.With("no such record at that version")
            : new SourceRecord(found.Ref, RecordKind.Doc, found.Title, FetchBlocked ? string.Empty : found.Text, Author: null, CreatedAt: null, UpdatedAt: null)
            {
                Blocked = FetchBlocked,
            };
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refs);
        var failure = Failure ?? (VersionsFail ? ContextErrors.SourceUnavailable.With("the version check is down") : null);
        return ValueTask.FromResult<Result<IReadOnlyList<SourceVersion?>>>(failure is null ? refs.Select(r => _current.GetValueOrDefault(r.Id)).ToList() : failure);
    }

    /// <inheritdoc />
    public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) => ValueTask.FromResult(Failure is { } failure
        ? new SourceHealth(SourceState.Unavailable, "fake set to fail", failure.Code)
        : new SourceHealth(SourceState.Ok, "fake", Code: null));
}
