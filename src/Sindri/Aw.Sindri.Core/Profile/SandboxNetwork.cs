using System.Text.Json.Serialization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'sandbox.network' keys (sandbox spec §3.2, §9).</summary>
/// <param name="Groups">Named host groups: group name to host patterns ('host' or '*.host').</param>
/// <param name="Mode">'enforce' or 'learn' (owner decision Q13).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SandboxNetwork(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, IReadOnlyList<string>>? Groups = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NetworkMode? Mode = null);
