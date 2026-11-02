using System.Text.Json.Serialization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'sandbox.login' keys (sandbox spec SB18).</summary>
/// <param name="Source">The profile-wide model credential source (SB18).</param>
/// <param name="Targets">Per-agent overrides by agent id.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SandboxLogin(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ModelLoginSource? Source = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, ModelLoginSource>? Targets = null);
