using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>Whether a finding refuses the stage (fail) or is only shown (warn).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LineSeverity>))]
public enum LineSeverity
{
    /// <summary>The stage's outcome carries it.</summary>
    [JsonStringEnumMemberName("fail")]
    Fail = 1,

    /// <summary>Shown at the stop; never changes an outcome and is never a fact:findings entry.</summary>
    [JsonStringEnumMemberName("warn")]
    Warn = 2,
}
