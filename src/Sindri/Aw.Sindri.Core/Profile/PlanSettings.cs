using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.plan:' block (line spec §6.3, Appendix D).</summary>
/// <param name="Sizes">The limits of each size class (XS, S, M, L, XL) written to the sizes file aw-packets reads; a size left out takes its default.</param>
/// <param name="MaxPhaseSize">The largest size a phase may be: XS, S or M (default M; PLN-SIZE-001).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlanSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, SizeSettings>? Sizes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MaxPhaseSize = null);
