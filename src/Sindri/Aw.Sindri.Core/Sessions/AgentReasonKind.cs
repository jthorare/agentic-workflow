using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Which of the Agents page's reasons holds (Agents page spec §3.2). The class number says where it sorts; this says what it is.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentReasonKind>))]
public enum AgentReasonKind
{
    /// <summary>Class 1: a tool needs approval.</summary>
    [JsonStringEnumMemberName("permission-prompt")]
    PermissionPrompt,

    /// <summary>Class 1: the agent asked a question.</summary>
    [JsonStringEnumMemberName("owner-question")]
    OwnerQuestion,

    /// <summary>Class 1: the agent needs a login.</summary>
    [JsonStringEnumMemberName("login")]
    Login,

    /// <summary>Class 2 (Waiting group): the lane hit its usage limit.</summary>
    [JsonStringEnumMemberName("usage-limit")]
    UsageLimit,

    /// <summary>Class 3: a lane in the team map has no live session.</summary>
    [JsonStringEnumMemberName("lane-no-session")]
    LaneNoSession,

    /// <summary>Class 4: the lane's context is at the recycle mark.</summary>
    [JsonStringEnumMemberName("recycle-mark")]
    RecycleMark,

    /// <summary>Class 5: the lane has been idle past the limit while work is owed to it.</summary>
    [JsonStringEnumMemberName("idle-owed")]
    IdleOwed,

    /// <summary>Class 6 (Waiting group): a heavy-slot waiter waited past the limit.</summary>
    [JsonStringEnumMemberName("slot-wait")]
    SlotWait,

    /// <summary>Class 7: the lane's PR waits for the owner to merge it.</summary>
    [JsonStringEnumMemberName("pr-ready")]
    PrReady,
}
