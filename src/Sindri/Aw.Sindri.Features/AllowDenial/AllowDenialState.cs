using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.AllowDenial;

/// <summary>Where an allowance stands after the profile was edited.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AllowDenialState>))]
public enum AllowDenialState
{
    /// <summary>The live profile has it and the owner hasn't approved yet.</summary>
    [JsonStringEnumMemberName("needs-approval")]
    NeedsApproval,

    /// <summary>The approved profile already has it.</summary>
    [JsonStringEnumMemberName("allowed")]
    Allowed,
}
