using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Chunking;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Sync;

/// <summary>
/// The step between a feed's page and the store (spec §5.5): the sync driver cleans each item's text at the ingest
/// boundary, windows it with <see cref="Chunker"/> and hands the store a <see cref="RecordBatch"/>. Pure given the
/// scrubbers, so every feed shares it and a replay of the same page builds the same batch.
/// </summary>
public static class RecordBatches
{
    /// <summary>The most characters of a project's description a record keeps (the text's chunks hold the rest for search).</summary>
    public const int MaxDescription = 4000;

    /// <summary>Builds the batch for one page.</summary>
    /// <param name="source">The source the page came from.</param>
    /// <param name="page">The page; its cursor becomes the position.</param>
    /// <param name="phase">The phase the next pull belongs to.</param>
    /// <param name="scrubbers">The call's scrubbers; their ruleset is stored on every record.</param>
    /// <param name="at">The driver's clock: the records' sync time and a tombstone's removal time.</param>
    /// <returns>The batch: a blocked item is a record with no chunks, an item with empty clean text a record with none either.</returns>
    public static RecordBatch From(SourceId source, RecordPage page, SyncPhase phase, Scrubbers scrubbers, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(scrubbers);
        var writes = page.Items.Select(item => Write(source, item, scrubbers, at)).ToList();
        return new RecordBatch(source, writes, page.Removed, new SyncPosition(page.Next, phase, BackfillCursor: null), at);
    }

    private static RecordWrite Write(SourceId source, RecordItem item, Scrubbers scrubbers, DateTimeOffset at)
    {
        var clean = scrubbers.Ingest.Clean(item.Text, singleLine: false);
        var title = scrubbers.Ingest.Clean(item.Title, singleLine: true);
        var record = new StoredRecord(
            item.Key, source, item.Kind, item.Repo, item.Audience,
            clean.Blocked || title.Blocked ? item.Key.Value : UntrustedFence.Cap(title.Text),
            item.Url, item.Author, item.State, item.StateRaw, item.CreatedAt, item.UpdatedAt, item.ClosedAt, item.Version,
            Hash(clean.Text), scrubbers.Ruleset.Value, clean.Blocked, RemovedAt: null, at, item.Links, item.Health, item.DueAt);
        IReadOnlyList<RecordChunk> chunks = clean.Blocked
            ? []
            : [.. Chunker.Split(clean.Text).Select(c => new RecordChunk(c.Seq, c.Text, c.Tokens, Hash(c.Text), c.LineStart, c.LineEnd))];
        return new RecordWrite(record, chunks);
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
