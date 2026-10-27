using System.Text.Json.Serialization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'sandbox.credentials' keys (sandbox spec §3.4).</summary>
/// <param name="Github">'none' or 'agent-app'.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SandboxCredentials(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GithubCredentials? Github = null);
