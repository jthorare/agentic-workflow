using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Setup;

/// <summary>Which build a running map step waits on (#3112): the first code index of a mapped repo, or the map build itself; written in lower case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MapStage>))]
public enum MapStage
{
    /// <summary>A code index build of a mapped repo runs; the first map builds after it.</summary>
    [JsonStringEnumMemberName("index")]
    Index,

    /// <summary>The map build runs.</summary>
    [JsonStringEnumMemberName("map")]
    Map,
}
