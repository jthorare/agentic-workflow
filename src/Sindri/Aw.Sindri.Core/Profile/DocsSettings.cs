using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.docs:' block (line spec §7.6, Appendix D).</summary>
/// <param name="Globs">The paths docs sync may write (default docs/** and *.md): globs relative to the repo.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocsSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Globs = null);
