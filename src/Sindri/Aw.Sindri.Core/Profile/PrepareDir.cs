using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>One package directory of the prepare.</summary>
/// <param name="Dir">The directory, relative to the repo.</param>
/// <param name="Tool">The tool: 'npm ci'.</param>
/// <param name="AllowScripts">The packages whose install scripts may run (the repo's own allowScripts).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrepareDir(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Dir = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Tool = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? AllowScripts = null);
