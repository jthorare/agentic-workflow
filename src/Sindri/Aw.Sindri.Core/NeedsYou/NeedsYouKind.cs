using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>The kind of a needs-you row (API spec §3.6).</summary>
[JsonConverter(typeof(StrictEnumConverter<NeedsYouKind>))]
public enum NeedsYouKind
{
    /// <summary>A pending decision.</summary>
    [JsonStringEnumMemberName("decision")]
    Decision,

    /// <summary>A parked or escalated instance.</summary>
    [JsonStringEnumMemberName("park")]
    Park,

    /// <summary>A background service whose last run failed.</summary>
    [JsonStringEnumMemberName("failure")]
    Failure,

    /// <summary>The live profile changed since approval.</summary>
    [JsonStringEnumMemberName("profile")]
    Profile,

    /// <summary>A setup step that waits for the owner or failed (setup-once spec §7.3).</summary>
    [JsonStringEnumMemberName("setup")]
    Setup,

    /// <summary>A changed workflow that waits for its own approval (setup-once spec §5.3, §7.3).</summary>
    [JsonStringEnumMemberName("recipe")]
    Recipe,

    /// <summary>A connection whose token could not be refreshed and has to be signed in again (connections PR 10b).</summary>
    [JsonStringEnumMemberName("connection")]
    Connection,

    /// <summary>A note a graph 'notify' node posted; it clears when its run ends.</summary>
    [JsonStringEnumMemberName("workflow-notify")]
    WorkflowNotify,

    /// <summary>A question a graph 'ask_owner' node asked; it clears when it is answered or its run ends.</summary>
    [JsonStringEnumMemberName("question")]
    Question,

    /// <summary>A lane whose context is at or over the recycle mark (Home spec §3.2).</summary>
    [JsonStringEnumMemberName("lane-recycle")]
    LaneRecycle,

    /// <summary>A lane or agent that hit a usage limit (Home spec §3.2).</summary>
    [JsonStringEnumMemberName("agent-limited")]
    AgentLimited,

    /// <summary>A PR in the merge queue that waits for the owner to merge it (Home spec §3.2).</summary>
    [JsonStringEnumMemberName("pr-ready-merge")]
    PrReadyMerge,
}
