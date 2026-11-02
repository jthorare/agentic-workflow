using System.Text.Json.Serialization;

namespace Aw.Context.Core.Records;

/// <summary>What a stored vector belongs to (spec §4.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<VectorSpace>))]
public enum VectorSpace
{
    /// <summary>A record chunk; the key is the chunk key.</summary>
    [JsonStringEnumMemberName("chunk")]
    Chunk,

    /// <summary>A graph node; the key is the node key.</summary>
    [JsonStringEnumMemberName("node")]
    Node,
}
