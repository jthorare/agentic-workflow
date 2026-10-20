using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The lens packs (line spec §5.2): the design lenses and the review lenses.</summary>
/// <param name="Design">The design lens pack: default and per-repo.</param>
/// <param name="Review">The review lens pack: default and per-repo.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LensPackSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PackChoice? Design = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PackChoice? Review = null);
