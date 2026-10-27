using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>Which limit ended a session (sandbox spec §3.3): carried by 'session.ended' when its reason is <see cref="SessionEndReason.Limit"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LimitKind>))]
public enum LimitKind
{
    /// <summary>The scope's memory ceiling; <see cref="SessionEnded.Limit"/> and <see cref="SessionEnded.Peak"/> are bytes.</summary>
    [JsonStringEnumMemberName("memory")]
    Memory,

    /// <summary>The wall clock (suspended time not counted); <see cref="SessionEnded.Limit"/> and <see cref="SessionEnded.Peak"/> are seconds.</summary>
    [JsonStringEnumMemberName("wall")]
    Wall,
}
