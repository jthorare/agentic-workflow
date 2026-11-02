using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>What was denied (sandbox spec §3.6).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DenialKind>))]
public enum DenialKind
{
    /// <summary>A host the proxy refused.</summary>
    [JsonStringEnumMemberName("network")]
    Network,

    /// <summary>A tool the inner policy refused.</summary>
    [JsonStringEnumMemberName("policy")]
    Policy,

    /// <summary>A path that failed because it is denied or not bound (a likely denial).</summary>
    [JsonStringEnumMemberName("path")]
    Path,
}
