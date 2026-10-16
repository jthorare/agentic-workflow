using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.Decisions;

/// <summary>Where a decision stands.</summary>
[JsonConverter(typeof(StrictEnumConverter<DecisionState>))]
public enum DecisionState
{
    /// <summary>Waiting for a human.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending,

    /// <summary>A human chose an option.</summary>
    [JsonStringEnumMemberName("decided")]
    Decided,

    /// <summary>Its item was closed first.</summary>
    [JsonStringEnumMemberName("voided")]
    Voided,
}
