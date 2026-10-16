using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.Decisions;

/// <summary>What a decision decides; kebab case. P4 has one kind (later phases add theirs).</summary>
[JsonConverter(typeof(StrictEnumConverter<DecisionKind>))]
public enum DecisionKind
{
    /// <summary>Accept or decline a filed request.</summary>
    [JsonStringEnumMemberName("accept-request")]
    AcceptRequest,
}
