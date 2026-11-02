using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'sandbox.attended' keys (sandbox spec §3.9).</summary>
/// <param name="Wall">A wall-clock ceiling for sessions the owner opens: 'off' or a duration.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SandboxAttended(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Wall = null);
