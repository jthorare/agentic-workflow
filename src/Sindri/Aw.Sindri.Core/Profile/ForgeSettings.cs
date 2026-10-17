using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's 'workflow.forge:' block (PR babysitter spec W27, §10.2): which forge adapter the services and effects
/// use, the hosts it may reach and where its credential comes from. The credential itself is never in the profile.
/// </summary>
/// <param name="Adapter">'github' or 'file' (the file-backed forge tests and proofs use). Absent: the services that need a forge are not ready.</param>
/// <param name="Hosts">The forge hosts a link or request may name (for example github.com); a host outside the list is refused before anything is sent.</param>
/// <param name="Credential">'env:NAME' (a variable such as AW_FORGE_TOKEN) or 'command:&lt;argv&gt;' (run per call); default env:AW_FORGE_TOKEN.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ForgeSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Adapter = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Hosts = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Credential = null);
