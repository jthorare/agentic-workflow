using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>What a state's timeout does (spec §3.4, §3.8).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TimeoutAction>))]
public enum TimeoutAction
{
    /// <summary>Parks with reason 'timeout'.</summary>
    [JsonStringEnumMemberName("park")]
    Park = 1,

    /// <summary>Escalates to a human.</summary>
    [JsonStringEnumMemberName("escalate")]
    Escalate = 2,

    /// <summary>Re-arms the deadline in the same frame entry and re-emits the entering transition's effects; a second timeout parks.</summary>
    [JsonStringEnumMemberName("retry")]
    Retry = 3,
}
