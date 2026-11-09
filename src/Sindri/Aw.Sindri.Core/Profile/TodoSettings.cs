using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's top-level 'todo:' block (todo spec §4.5). Left out of the canonical JSON when absent, so a profile without it keeps its hash.</summary>
/// <param name="Obsidian">The one Obsidian note the todos sync with; null: no sync.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TodoSettings([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TodoObsidianSettings? Obsidian = null);
