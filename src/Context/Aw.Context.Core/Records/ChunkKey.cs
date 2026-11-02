using System.Globalization;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Records;

/// <summary>A chunk's key, '&lt;record key&gt;#&lt;seq&gt;' (spec §4.3): the record is everything before the last '#', because a record key may hold one itself.</summary>
/// <param name="Record">The record the chunk belongs to.</param>
/// <param name="Seq">The chunk's position in the record, from 0.</param>
public readonly record struct ChunkKey(RecordKey Record, int Seq)
{
    /// <summary>The key as stored.</summary>
    /// <returns>'&lt;record key&gt;#&lt;seq&gt;'.</returns>
    public override string ToString() => $"{Record.Value}#{Seq.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Reads a stored chunk key: the cut is at the last '#', and what follows must be a number.</summary>
    /// <param name="text">The stored key.</param>
    /// <param name="key">The chunk key when the text is one.</param>
    /// <returns>True when the text is a chunk key.</returns>
    public static bool TryParse(string? text, out ChunkKey key)
    {
        key = default;
        var cut = text?.LastIndexOf('#') ?? -1;
        if (text is null || cut <= 0
            || !int.TryParse(text.AsSpan(cut + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var seq)
            || !RecordKey.TryFrom(text[..cut], out var record))
        {
            return false;
        }

        key = new ChunkKey(record, seq);
        return true;
    }
}
