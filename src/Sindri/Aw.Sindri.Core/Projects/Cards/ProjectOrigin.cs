using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>Where a card comes from (projects page spec §3.2): it decides which actions the sheet offers.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectOrigin>))]
public enum ProjectOrigin
{
    /// <summary>A Linear project the owner leads.</summary>
    [JsonStringEnumMemberName("led")]
    Led,

    /// <summary>A Linear project the owner attached.</summary>
    [JsonStringEnumMemberName("attached")]
    Attached,

    /// <summary>A project the owner added by hand.</summary>
    [JsonStringEnumMemberName("handMade")]
    HandMade,
}
