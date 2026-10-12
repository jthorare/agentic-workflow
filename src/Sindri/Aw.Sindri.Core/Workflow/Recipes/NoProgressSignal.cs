using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A signal that a loop made no progress (spec §3.7, A §17.23.13).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NoProgressSignal>))]
public enum NoProgressSignal
{
    /// <summary>The same findings as the previous round.</summary>
    [JsonStringEnumMemberName("findings")]
    Findings = 1,

    /// <summary>The round changed nothing.</summary>
    [JsonStringEnumMemberName("emptyDiff")]
    EmptyDiff = 2,

    /// <summary>The same failure as the previous round.</summary>
    [JsonStringEnumMemberName("sameFailure")]
    SameFailure = 3,
}
