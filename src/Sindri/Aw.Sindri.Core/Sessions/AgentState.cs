using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sessions;

/// <summary>What an Agents row is doing (Agents page spec §3.1): the five states of the page's glyphs. A row with an estimated or unreadable state still has one of these; the page words the guess.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentState>))]
public enum AgentState
{
    /// <summary>A turn is running (an arc).</summary>
    [JsonStringEnumMemberName("working")]
    Working,

    /// <summary>The session asks for approval, input or a login, or a workflow node waits on the owner (a diamond).</summary>
    [JsonStringEnumMemberName("waiting-on-you")]
    WaitingOnYou,

    /// <summary>The turn ended and the state is not a guess (a ring).</summary>
    [JsonStringEnumMemberName("idle")]
    Idle,

    /// <summary>The lane hit its usage limit, or a Sindri run paused on a model limit (a left half disc).</summary>
    [JsonStringEnumMemberName("limited")]
    Limited,

    /// <summary>A lane in the team map has no live session, or a session's process left with no end event (a bare x).</summary>
    [JsonStringEnumMemberName("down")]
    Down,
}
