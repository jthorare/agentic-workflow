using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>Where an entry's session id comes from (launcher spec section 4, 'sessionId').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SessionIdMode>))]
public enum SessionIdMode
{
    /// <summary>The entry has no session id; Resume is never offered.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Sindri picks a UUID and passes it in the open template.</summary>
    [JsonStringEnumMemberName("pre-chosen-uuid")]
    PreChosenUuid,

    /// <summary>The CLI reports its own id through its hook once it is up.</summary>
    [JsonStringEnumMemberName("reported")]
    Reported,
}
