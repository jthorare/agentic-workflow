using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>A size class's limits (line spec §6.3, PKT-SIZE-001): what a phase of that size may hold.</summary>
/// <param name="Files">The most files its write scope covers.</param>
/// <param name="Items">The most acceptance items it holds, hidden scenarios included.</param>
/// <param name="Budget">The most it may spend.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SizeSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Files = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Items = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BudgetSettings? Budget = null);
