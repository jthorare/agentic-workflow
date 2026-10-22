using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Planning;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Similarity;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

/// <summary>
/// The structure step of a build (spec §5.7 step 3): reads each planned file's blob from git objects, parses it behind the crash guard,
/// scrubs signatures and writes the files in batches of 200 with a counts-only progress line each time.
/// </summary>
public sealed class StructureWriter
{
    private static readonly HashSet<SymbolKind> _classLike = [SymbolKind.Class, SymbolKind.Record, SymbolKind.Struct, SymbolKind.Interface, SymbolKind.Enum];

    private readonly IRepoSnapshot _snapshot;
    private readonly ISymbolParser[] _parsers;
    private readonly IParseQuarantine _quarantine;
    private readonly IBodyScrubber _scrubber;

    /// <summary>Creates the writer over its ports.</summary>
    /// <param name="snapshot">The repo's blobs.</param>
    /// <param name="parsers">The parsers, chosen by file extension.</param>
    /// <param name="quarantine">The crash guard around parsing.</param>
    /// <param name="scrubber">Scrubs signatures before they are stored.</param>
    public StructureWriter(IRepoSnapshot snapshot, IEnumerable<ISymbolParser> parsers, IParseQuarantine quarantine, IBodyScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(parsers);
        _snapshot = snapshot;
        _parsers = [.. parsers];
        _quarantine = quarantine;
        _scrubber = scrubber;
    }

    /// <summary>The parsers' versions, joined; part of the structure stamp.</summary>
    public string ParserVersion => string.Join('+', _parsers.Select(p => p.Version).Order(StringComparer.Ordinal));

    /// <summary>Moves a crashed run's mark onto the quarantine list and drops entries no longer in the head.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="blobsInHead">Every blob id of the snapshot being indexed.</param>
    public void Prepare(RepoName repo, IReadOnlySet<string> blobsInHead)
    {
        _ = _quarantine.Recover(repo);
        _ = _quarantine.Keep(repo, blobsInHead);
    }

    /// <summary>Drops the planned files and writes the files to parse.</summary>
    /// <param name="build">The build file.</param>
    /// <param name="request">The request (repo and progress).</param>
    /// <param name="plan">What to parse and drop.</param>
    /// <param name="cancellationToken">Cancels between files.</param>
    /// <returns>Success, or the first failure.</returns>
    public async ValueTask<Result<Unit>> Write(IIndexBuild build, BuildCodeIndexRequest request, BuildPlan plan, CancellationToken cancellationToken)
    {
        if (plan.ToDrop.Count > 0)
        {
            var dropped = await build.RemoveFiles(plan.ToDrop, cancellationToken).ConfigureAwait(false);
            if (dropped.IsError)
            {
                return dropped.Error;
            }
        }

        if (plan.ToParse.Count == 0)
        {
            return Unit.Value;
        }

        var blobs = await _snapshot.OpenBlobs(request.Repo, cancellationToken).ConfigureAwait(false);
        if (blobs.IsError)
        {
            return blobs.Error;
        }

        await using (blobs.Value.ConfigureAwait(false))
        {
            var done = 0;
            foreach (var batch in plan.ToParse.Chunk(CodeIndexRules.FileBatchSize))
            {
                var written = await WriteBatch(blobs.Value, build, request, batch, cancellationToken).ConfigureAwait(false);
                if (written.IsError)
                {
                    return written.Error;
                }

                done += batch.Length;
                request.Progress.Report($"structure {Counts.Format(done)} of {Counts.Format(plan.ToParse.Count)} files");
            }
        }

        return Unit.Value;
    }

    private async ValueTask<Result<Unit>> WriteBatch(IBlobReader blobs, IIndexBuild build, BuildCodeIndexRequest request, SnapshotFile[] batch, CancellationToken cancellationToken)
    {
        var records = new List<FileRecord>(batch.Length);
        foreach (var file in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = await Read(blobs, request.Repo, file, cancellationToken).ConfigureAwait(false);
            if (record.IsError)
            {
                return record.Error;
            }

            records.Add(record.Value);
        }

        return await build.WriteFiles(records, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<FileRecord>> Read(IBlobReader blobs, CodeIndexRepo repo, SnapshotFile file, CancellationToken cancellationToken)
    {
        var language = LanguageTable.Of(file.Path) ?? "other";
        var unparsed = new FileRecord(file.Path, file.Blob, file.Size, language, Parsed: false, []);
        var parser = _parsers.FirstOrDefault(p => p.Handles(file.Path));
        if (parser is null || _quarantine.Contains(repo.Name, file.Blob))
        {
            return unparsed;
        }

        var bytes = await blobs.Read(file.Blob, cancellationToken).ConfigureAwait(false);
        if (bytes.IsError)
        {
            return bytes.Error;
        }

        if (bytes.Value is null)
        {
            return unparsed;
        }

        var outcome = Parse(parser, repo.Name, file, Encoding.UTF8.GetString(bytes.Value), cancellationToken);
        return outcome is { Skip: null } parsed ? new FileRecord(file.Path, file.Blob, file.Size, language, Parsed: true, Records(repo, file.Path, parsed.Symbols)) : unparsed;
    }

    private ParseOutcome? Parse(ISymbolParser parser, RepoName repo, SnapshotFile file, string text, CancellationToken cancellationToken)
    {
        _quarantine.Begin(repo, file.Blob);
        try
        {
            var result = parser.Parse(file.Path, text, cancellationToken);
            return result.IsSuccess ? result.Value : null;
        }
        finally
        {
            _quarantine.End(repo);
        }
    }

    private List<SymbolRecord> Records(CodeIndexRepo repo, string path, IReadOnlyList<ParsedSymbol> symbols)
    {
        var refs = SymbolRefs.Assign(repo.Name, path, symbols);
        var utility = InputRules.MatchesAny(path, repo.Settings.UtilityGlobs);
        var records = new List<SymbolRecord>(symbols.Count);
        for (var i = 0; i < symbols.Count; i++)
        {
            if (refs[i] is not { } reference)
            {
                continue;
            }

            var s = symbols[i];
            var body = StoredBody(repo.Settings, s);
            var minHash = Clones.Signature(s.Tokens);
            records.Add(new SymbolRecord(
                reference, s.Name, s.Qualified, s.Kind, s.Arity, s.StartLine, s.EndLine, s.Exported, utility,
                _scrubber.Scrub(s.Signature, CodeIndexRules.SignatureMaxCharacters), s.ShapeHash, s.Tokens.Count, s.Complexity,
                minHash, Clones.BandKeys(minHash), body, s.CallSites));
        }

        return records;
    }

    // The text kept until the symbol is embedded (spec §4.2, Q7): scrubbed, then cut, and only while the embeddings layer is on;
    // a class-like symbol is never embedded, so its text is never kept. A symbol with no body of its own (a field, an
    // enum member) keeps an empty one, so its name and signature are still embedded and the layer can finish.
    private string? StoredBody(CodeIndexSettings settings, ParsedSymbol symbol)
        => !settings.Embeddings.Enabled || _classLike.Contains(symbol.Kind) ? null : _scrubber.Scrub(symbol.Body ?? string.Empty, CodeIndexRules.EmbedTextMaxCharacters);
}
