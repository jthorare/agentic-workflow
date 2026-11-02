using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;

namespace Aw.Context.Testing.Records;

/// <summary>Synthetic records for the record-store contract and the tests that build on it: one source, a fixed clock, chunks hashed the way the store compares them.</summary>
public static class RecordSamples
{
    /// <summary>The source every sample belongs to.</summary>
    public static SourceId Linear { get; } = SourceId.From("linear");

    /// <summary>A second source, to check that a purge or a batch stays inside one.</summary>
    public static SourceId Github { get; } = SourceId.From("github");

    /// <summary>The instant every sample is written at.</summary>
    public static DateTimeOffset Now { get; } = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The sha-256 of a text, lower-case hex.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The hash.</returns>
    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>A record of the Linear source with a given version and text hash.</summary>
    /// <param name="key">The record key.</param>
    /// <param name="version">The source's version.</param>
    /// <param name="source">The source it came from.</param>
    /// <param name="blocked">Whether the scrubber withheld it.</param>
    /// <returns>The row.</returns>
    public static StoredRecord Record(string key, string version = "v1", SourceId? source = null, bool blocked = false) => new(
        RecordKey.From(key), source ?? Linear, RecordKind.Issue, Repo: null, Audience: AudienceKey.From("linear:team-a"), blocked ? key : $"Title of {key}", Url: null, Author: null,
        RecordState.Open, StateRaw: "In Review", CreatedAt: null, UpdatedAt: Now, ClosedAt: null, SourceVersion.From(version), Hash(key + version), Ruleset: "r1", blocked, RemovedAt: null, Now, Links: []);

    /// <summary>A write of a record with one chunk per text.</summary>
    /// <param name="record">The row.</param>
    /// <param name="texts">The chunk texts, in order.</param>
    /// <returns>The write.</returns>
    public static RecordWrite Write(StoredRecord record, params string[] texts) =>
        new(record, [.. texts.Select((text, seq) => new RecordChunk(seq, text, (text.Length / 4) + 1, Hash(text)))]);

    /// <summary>A batch of one page of a source, the position after it a cursor named after the page.</summary>
    /// <param name="source">The source.</param>
    /// <param name="page">The page number: the cursor after it.</param>
    /// <param name="writes">The records.</param>
    /// <returns>The batch.</returns>
    public static RecordBatch Batch(SourceId source, int page, params RecordWrite[] writes) =>
        new(source, writes, [], new SyncPosition(SourceCursor.From(page.ToString(CultureInfo.InvariantCulture)), SyncPhase.Incremental, BackfillCursor: null), Now);

    /// <summary>A vector write for a chunk, normalised.</summary>
    /// <param name="chunk">The chunk key.</param>
    /// <param name="text">The text embedded (its hash is compared with the chunk's).</param>
    /// <param name="components">The direction.</param>
    /// <returns>The write.</returns>
    public static VectorWrite Vector(ChunkKey chunk, string text, params float[] components)
    {
        var norm = MathF.Sqrt(components.Sum(c => c * c));
        return new VectorWrite(VectorSpace.Chunk, chunk.ToString(), "fake-model@1", Hash(text), components.Select(c => c / norm).ToArray());
    }
}
