using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A state's kind (spec §3.4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StateKind>))]
public enum StateKind
{
    /// <summary>Runs a Step.</summary>
    [JsonStringEnumMemberName("active")]
    Active = 1,

    /// <summary>Waits for an event, an answer, a resource or a child.</summary>
    [JsonStringEnumMemberName("wait")]
    Wait = 2,

    /// <summary>Ends the instance with an outcome.</summary>
    [JsonStringEnumMemberName("terminal")]
    Terminal = 3,
}
