using System.Buffers.Binary;
using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The record store's row mappers and the binds its statements share.</summary>
internal static class RecordMappers
{
    /// <summary>How a due day is stored: the date alone, no time zone.</summary>
    internal const string DueFormat = "yyyy-MM-dd";

    internal static IRowMapper<StoredRecord> Record { get; } = new Mapper<StoredRecord>(r => new StoredRecord(
        RecordKey.From(r.GetString(0)),
        SourceId.From(r.GetString(1)),
        RecordWire.Kind(r.GetString(2)),
        Text(r, 3) is { } repo ? ContextRepoName.From(repo) : null,
        Text(r, 4) is { } audience ? AudienceKey.From(audience) : null,
        r.GetString(5),
        Text(r, 6),
        Text(r, 7),
        Text(r, 8) is { } state ? RecordWire.State(state) : null,
        Text(r, 9),
        At(r, 10),
        At(r, 11),
        At(r, 12),
        SourceVersion.From(r.GetString(13)),
        r.GetString(14),
        r.GetString(15),
        r.GetInt64(16) == 1,
        At(r, 17),
        SqliteRead.GetTimestamp(r, 18),
        [],
        Text(r, 19),
        Text(r, 20) is { } due ? DateOnly.ParseExact(due, DueFormat, CultureInfo.InvariantCulture) : null));

    internal static IRowMapper<SourceIdentity> Identity { get; } = new Mapper<SourceIdentity>(r =>
        new SourceIdentity(SourceId.From(r.GetString(0)), r.GetString(1), r.GetString(2), SqliteRead.GetTimestamp(r, 3)));

    internal static IRowMapper<RecordLink> Link { get; } = new Mapper<RecordLink>(r =>
        new RecordLink(r.GetString(0), RecordWire.Link(r.GetString(1)), Text(r, 2), Text(r, 3) is { } to ? RecordKey.From(to) : null));

    internal static IRowMapper<ChunkToEmbed> ToEmbed { get; } = new Mapper<ChunkToEmbed>(r => new ChunkToEmbed(r.GetString(0), r.GetString(1), r.GetString(2)));

    internal static IRowMapper<RecordChunk> Chunk { get; } = new Mapper<RecordChunk>(r =>
        new RecordChunk(r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetInt32(4), r.IsDBNull(5) ? null : r.GetInt32(5)));

    internal static IRowMapper<SyncPosition> Position { get; } = new Mapper<SyncPosition>(r =>
        new SyncPosition(Text(r, 0) is { } cursor ? SourceCursor.From(cursor) : null, Text(r, 1) is { } phase ? RecordWire.Phase(phase) : null, Text(r, 2) is { } backfill ? SourceCursor.From(backfill) : null, At(r, 3)));

    internal static IRowMapper<SyncOutcome> Outcome { get; } = new Mapper<SyncOutcome>(r => new SyncOutcome(SqliteRead.GetTimestamp(r, 0), Text(r, 1)));

    internal static IRowMapper<SourceBackoffState> Backoff { get; } = new Mapper<SourceBackoffState>(r => new SourceBackoffState(r.GetInt32(0), At(r, 1), At(r, 2), Text(r, 3)));

    internal static IRowMapper<SourceCounts> Counts { get; } = new Mapper<SourceCounts>(r => new SourceCounts(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2)));

    internal static IRowMapper<StoreStamps> Stamps { get; } = new Mapper<StoreStamps>(r => new StoreStamps(r.GetInt64(0), r.GetInt64(1)));

    internal static IRowMapper<ExistingRecord> Existing { get; } = new Mapper<ExistingRecord>(r =>
        new ExistingRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt64(3) == 1, !r.IsDBNull(4)));

    /// <summary>A vector row of one space (the space is the query's, not a column).</summary>
    /// <param name="space">The space the query asked for.</param>
    /// <returns>The mapper.</returns>
    internal static IRowMapper<VectorRow> Vector(VectorSpace space) => new Mapper<VectorRow>(r =>
    {
        var bytes = r.GetFieldValue<byte[]>(5);
        var floats = new float[bytes.Length / sizeof(float)];
        for (var i = 0; i < floats.Length; i++)
        {
            floats[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float), sizeof(float)));
        }

        return new VectorRow(r.GetInt64(0), new VectorWrite(space, r.GetString(1), r.GetString(2), r.GetString(3), floats));
    });

    /// <summary>A vector's float32 little-endian bytes (the column's format, spec §4.2).</summary>
    /// <param name="vec">The components.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] Bytes(ReadOnlySpan<float> vec)
    {
        var bytes = new byte[vec.Length * sizeof(float)];
        for (var i = 0; i < vec.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float), sizeof(float)), vec[i]);
        }

        return bytes;
    }

    /// <summary>Binds an instant as UTC round-trip text, or NULL.</summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter.</param>
    /// <param name="at">The instant, or null.</param>
    /// <returns>The command.</returns>
    internal static SqliteCommand BindAt(this SqliteCommand command, string name, DateTimeOffset? at) =>
        at is { } value ? command.Bind(name, value) : command.Bind(name, (string?)null);

    private static string? Text(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetString(ordinal);

    private static DateTimeOffset? At(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : SqliteRead.GetTimestamp(r, ordinal);

    private sealed class Mapper<T>(Func<SqliteDataReader, T> map) : IRowMapper<T>
    {
        public T Map(SqliteDataReader reader) => map(reader);
    }
}
