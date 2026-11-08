using System.Buffers.Binary;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Memory;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Memory;

/// <summary>Maps a 'memory_fact' row (<see cref="MemorySql.FactColumns"/> order).</summary>
internal sealed class MemoryFactMapper : IRowMapper<MemoryFact>
{
    public static readonly MemoryFactMapper Instance = new();

    public MemoryFact Map(SqliteDataReader reader) => new(
        reader.GetString(0),
        new MemoryName { Value = reader.GetString(1) },
        reader.GetString(2),
        Tags(reader.GetString(4)),
        new MemoryProvenance(Text(reader, 5), Text(reader, 6), Text(reader, 7), Text(reader, 8)),
        reader.IsDBNull(9) ? null : SqliteRead.GetTimestamp(reader, 9),
        SqliteRead.GetTimestamp(reader, 10),
        SqliteRead.GetTimestamp(reader, 11),
        reader.IsDBNull(3) ? ReadOnlyMemory<float>.Empty : Decode(SqliteRead.GetBlob(reader, 3), reader.IsDBNull(13) ? 0 : reader.GetInt32(13)),
        Text(reader, 12));

    /// <summary>The stored form of a vector: float32, little-endian, no header (the format of Aw.Embeddings.VectorCodec, which Sindri reaches from the Host only).</summary>
    internal static byte[] Encode(ReadOnlySpan<float> vector)
    {
        var blob = new byte[vector.Length * sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(blob.AsSpan(i * sizeof(float)), vector[i]);
        }

        return blob;
    }

    private static float[] Decode(byte[] blob, int dimension)
    {
        // a blob that is not exactly dimension * 4 bytes is a data bug: refuse it as VectorCodec does, so a row never reads back as a different vector
        if (dimension < 1 || blob.Length != dimension * sizeof(float))
        {
            throw new InvalidDataException("A memory fact's vector blob does not match its dimension.");
        }

        var vector = new float[blob.Length / sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = BinaryPrimitives.ReadSingleLittleEndian(blob.AsSpan(i * sizeof(float)));
        }

        return vector;
    }

    private static string? Text(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static string[] Tags(string json) => TagJson.Read(json);
}
