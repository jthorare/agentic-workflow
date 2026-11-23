using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Messages;

/// <summary>Why a thread is mine (messages spec §7.1), in the spec's order.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MineReason>))]
public enum MineReason
{
    /// <summary>A direct message or group DM: always mine.</summary>
    [JsonStringEnumMemberName("directMessage")]
    DirectMessage,

    /// <summary>I wrote at least one message in the thread.</summary>
    [JsonStringEnumMemberName("wroteInThread")]
    WroteInThread,

    /// <summary>A message in it names me (&lt;@me&gt;), not a broadcast.</summary>
    [JsonStringEnumMemberName("namedMe")]
    NamedMe,

    /// <summary>I follow it (Release 2).</summary>
    [JsonStringEnumMemberName("followed")]
    Followed,
}
