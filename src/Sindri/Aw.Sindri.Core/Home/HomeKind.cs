using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.Home;

/// <summary>The kind of a Home row (home spec §3.2).</summary>
[JsonConverter(typeof(StrictEnumConverter<HomeKind>))]
public enum HomeKind
{
    /// <summary>A pending decision.</summary>
    [JsonStringEnumMemberName("decision")]
    Decision,

    /// <summary>An unanswered graph ask_owner question.</summary>
    [JsonStringEnumMemberName("question")]
    Question,

    /// <summary>A workflow run ended failed and was not seen.</summary>
    [JsonStringEnumMemberName("run-failed")]
    RunFailed,

    /// <summary>A setup, profile, connection or park row.</summary>
    [JsonStringEnumMemberName("system")]
    System,

    /// <summary>An agent session is asking.</summary>
    [JsonStringEnumMemberName("agent-asking")]
    AgentAsking,

    /// <summary>An agent is stopped on a usage limit.</summary>
    [JsonStringEnumMemberName("agent-limited")]
    AgentLimited,

    /// <summary>A lane is over its context limit.</summary>
    [JsonStringEnumMemberName("lane-recycle")]
    LaneRecycle,

    /// <summary>A pull request is ready to merge.</summary>
    [JsonStringEnumMemberName("pr-ready-merge")]
    PrReadyMerge,

    /// <summary>A review was requested from the owner.</summary>
    [JsonStringEnumMemberName("review-requested")]
    ReviewRequested,

    /// <summary>The owner reviewed and the PR has newer commits.</summary>
    [JsonStringEnumMemberName("review-new-commits")]
    ReviewNewCommits,

    /// <summary>A comment names the owner.</summary>
    [JsonStringEnumMemberName("mention")]
    Mention,

    /// <summary>A direct message waits for the owner.</summary>
    [JsonStringEnumMemberName("dm")]
    Dm,

    /// <summary>A reply in a thread the owner wrote in.</summary>
    [JsonStringEnumMemberName("thread-reply")]
    ThreadReply,

    /// <summary>A message matches one of the owner's keywords.</summary>
    [JsonStringEnumMemberName("keyword")]
    Keyword,

    /// <summary>An issue was assigned to the owner today.</summary>
    [JsonStringEnumMemberName("issue-assigned")]
    IssueAssigned,

    /// <summary>A todo is due today or overdue.</summary>
    [JsonStringEnumMemberName("todo-due")]
    TodoDue,
}
