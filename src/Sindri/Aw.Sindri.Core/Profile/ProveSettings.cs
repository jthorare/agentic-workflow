using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.prove:' block (line spec §7.3, Appendix D).</summary>
/// <param name="HoldoutMin">The fewest hidden scenarios a phase needs besides its canary (default 2; PLN-HOLD-003).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProveSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? HoldoutMin = null);
