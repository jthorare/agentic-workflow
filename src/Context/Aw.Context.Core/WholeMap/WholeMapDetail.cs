using System.Text.Json.Serialization;

namespace Aw.Context.Core.WholeMap;

/// <summary>How much of the whole map one answer carries (Part D, §2.1).</summary>
[JsonConverter(typeof(StrictEnumConverter<WholeMapDetail>))]
public enum WholeMapDetail
{
    /// <summary>Nodes when the map fits the node budget, else areas. Only a question says it; an answer never does.</summary>
    [JsonStringEnumMemberName("auto")]
    Auto,

    /// <summary>Every node and edge.</summary>
    [JsonStringEnumMemberName("nodes")]
    Nodes,

    /// <summary>One entry per area, with the edges between areas.</summary>
    [JsonStringEnumMemberName("areas")]
    Areas,
}
