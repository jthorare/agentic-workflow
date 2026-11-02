using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>How a mount is bound (a login mount, or a 'homeAllow' entry).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MountMode>))]
public enum MountMode
{
    /// <summary>Read-only.</summary>
    [JsonStringEnumMemberName("ro")]
    ReadOnly,

    /// <summary>Read-write.</summary>
    [JsonStringEnumMemberName("rw")]
    ReadWrite,
}
