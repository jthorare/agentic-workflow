using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A 'when: { fact, is }' test (spec §3.6).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FactPresence>))]
public enum FactPresence
{
    /// <summary>The fact is recorded.</summary>
    [JsonStringEnumMemberName("present")]
    Present = 1,

    /// <summary>The fact isn't recorded.</summary>
    [JsonStringEnumMemberName("absent")]
    Absent = 2,
}
