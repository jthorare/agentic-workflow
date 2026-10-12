using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>The bound of a 'when: { loop, below }' test (spec §3.6): the loop's own cap.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LoopBound>))]
public enum LoopBound
{
    /// <summary>Below the loop's cap.</summary>
    [JsonStringEnumMemberName("cap")]
    Cap = 1,
}
