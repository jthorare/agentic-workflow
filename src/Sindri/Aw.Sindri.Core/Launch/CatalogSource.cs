using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>Where a launched catalog entry came from (launcher spec L-10): recorded on 'launch.started'.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CatalogSource>))]
public enum CatalogSource
{
    /// <summary>A built-in entry, unchanged.</summary>
    [JsonStringEnumMemberName("builtin")]
    Builtin,

    /// <summary>A built-in entry that launcher.json replaced ("override": true).</summary>
    [JsonStringEnumMemberName("override")]
    Override,

    /// <summary>An entry launcher.json added.</summary>
    [JsonStringEnumMemberName("added")]
    Added,
}
