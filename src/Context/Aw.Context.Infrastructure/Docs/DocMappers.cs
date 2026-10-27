using Aw.Context.Core.Docs;
using Aw.Context.Core.Sources;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>The repo-docs row mappers, one per row shape.</summary>
internal static class DocMappers
{
    internal static IRowMapper<StoredDoc> Stored { get; } = new StoredMapper();

    internal static IRowMapper<DocTextRow> Text { get; } = new TextMapper();

    internal static IRowMapper<HitRow> Hit { get; } = new HitMapper();

    internal static IRowMapper<DocIndexStatus> Status { get; } = new StatusMapper();

    private sealed class StoredMapper : IRowMapper<StoredDoc>
    {
        public StoredDoc Map(SqliteDataReader reader) => new(reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    private sealed class TextMapper : IRowMapper<DocTextRow>
    {
        public DocTextRow Map(SqliteDataReader reader) =>
            new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2) != 0, reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private sealed class HitMapper : IRowMapper<HitRow>
    {
        public HitRow Map(SqliteDataReader reader) =>
            new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetString(5));
    }

    private sealed class StatusMapper : IRowMapper<DocIndexStatus>
    {
        public DocIndexStatus Map(SqliteDataReader reader) => new(
            reader.IsDBNull(0) ? null : SourceVersion.From(reader.GetString(0)),
            reader.IsDBNull(1) ? null : SqliteRead.GetTimestamp(reader, 1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetInt32(3),
            reader.GetInt32(4))
        {
            Outdated = reader.GetInt32(5),
        };
    }
}
