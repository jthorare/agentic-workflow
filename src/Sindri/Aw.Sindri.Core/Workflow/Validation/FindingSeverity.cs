using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>How much a finding counts (spec §9.1). Only <see cref="Error"/> fails validation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FindingSeverity>))]
public enum FindingSeverity
{
    /// <summary>The recipe is invalid.</summary>
    [JsonStringEnumMemberName("error")]
    Error = 1,

    /// <summary>Worth knowing, not a failure (RCP-RES-001: a resource with no allocator in this build).</summary>
    [JsonStringEnumMemberName("information")]
    Information = 2,

    /// <summary>A rule that couldn't run here (RCP-EFFECT-001 without the profile's effect allowlist).</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped = 3,
}
