using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>What a state timeout does when it fires.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TimeoutKind>))]
public enum TimeoutKind
{
    /// <summary>It parks the instance.</summary>
    [JsonStringEnumMemberName("parks")]
    Parks = 1,

    /// <summary>It escalates the instance.</summary>
    [JsonStringEnumMemberName("escalates")]
    Escalates = 2,
}
