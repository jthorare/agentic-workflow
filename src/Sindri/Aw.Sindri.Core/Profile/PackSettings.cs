using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.packs:' block (line spec §5, Appendix D): which style and lens packs a repo uses, and the pins of the packs a repo or the machine supplies.</summary>
/// <param name="Style">The style pack: default and per-repo.</param>
/// <param name="Lenses">The lens packs.</param>
/// <param name="Pins">The hash of each machine or repo pack that is not built in, by '&lt;kind&gt;/&lt;id&gt;' (kind style or lenses).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PackSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PackChoice? Style = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LensPackSettings? Lenses = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Pins = null);
