using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>The stage a line station's check runs at (line spec §4.3, §6.3): after every draft, or in the evidence step.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LineStage>))]
public enum LineStage
{
    /// <summary>The draft-stage rules.</summary>
    [JsonStringEnumMemberName("draft")]
    Draft = 1,

    /// <summary>Every rule, the draft ones included.</summary>
    [JsonStringEnumMemberName("evidence")]
    Evidence = 2,
}
