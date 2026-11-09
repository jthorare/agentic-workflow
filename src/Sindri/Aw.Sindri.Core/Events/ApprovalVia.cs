using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>How a recipe was approved (spec §4.1, W9).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApprovalVia>))]
public enum ApprovalVia
{
    /// <summary>A human approved it with 'sindri recipe approve', through the challenge.</summary>
    [JsonStringEnumMemberName("desk")]
    Desk = 1,

    /// <summary>A generated recipe approved by the packet freeze, from its base.</summary>
    [JsonStringEnumMemberName("freeze")]
    Freeze = 2,
}
