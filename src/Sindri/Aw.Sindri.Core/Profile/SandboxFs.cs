using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'sandbox.fs' keys (sandbox spec §3.1, §9).</summary>
/// <param name="HomeAllow">Home entries bound in, each read-only or read-write.</param>
/// <param name="DenyExtra">More denied paths (absolute).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SandboxFs(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<HomeAllowEntry>? HomeAllow = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? DenyExtra = null);
