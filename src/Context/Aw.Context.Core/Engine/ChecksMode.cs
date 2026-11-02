using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>What a map build does when its structure checks would refuse the map ('context.checks', map structure spec §4.3).</summary>
[JsonConverter(typeof(StrictEnumConverter<ChecksMode>))]
public enum ChecksMode
{
    /// <summary>A refusal stops the build before any write (the default).</summary>
    [JsonStringEnumMemberName("refuse")]
    Refuse,

    /// <summary>A refusal is saved as a degraded note instead.</summary>
    [JsonStringEnumMemberName("warn")]
    Warn,
}
