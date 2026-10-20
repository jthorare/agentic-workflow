using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>A size class's budget (line spec §6.2): what a phase of that size may spend.</summary>
/// <param name="Tokens">Tokens.</param>
/// <param name="Turns">Agent turns.</param>
/// <param name="WallMinutes">Wall-clock minutes.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BudgetSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Tokens = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Turns = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? WallMinutes = null);
