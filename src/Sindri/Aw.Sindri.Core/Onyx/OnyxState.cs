using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Where the managed Onyx stands; written in kebab case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OnyxState>))]
public enum OnyxState
{
    /// <summary>Nothing has been set up yet (P2 adds running and stopped).</summary>
    [JsonStringEnumMemberName("not-set-up")]
    NotSetUp,
}
