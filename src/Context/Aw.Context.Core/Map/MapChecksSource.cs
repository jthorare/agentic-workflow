using System.Text.Json.Serialization;

namespace Aw.Context.Core.Map;

/// <summary>Which map 'context map checks' measures.</summary>
[JsonConverter(typeof(StrictEnumConverter<MapChecksSource>))]
public enum MapChecksSource
{
    /// <summary>The current map.</summary>
    [JsonStringEnumMemberName("current")]
    Current,

    /// <summary>The open draft.</summary>
    [JsonStringEnumMemberName("draft")]
    Draft,

    /// <summary>The map a build would save, extracted in memory with no model call; nothing is written.</summary>
    [JsonStringEnumMemberName("build")]
    Build,
}
