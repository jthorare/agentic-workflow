using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The host-side prepare declaration (sandbox spec §3.8).</summary>
/// <param name="Dirs">The package directories to install on the host before the launch.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SandboxPrepare(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<PrepareDir>? Dirs = null);
