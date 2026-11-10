using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.http:' block: what a workflow 'http' node may reach.</summary>
/// <param name="Hosts">The hosts an 'http' node may send to, each a bare DNS name such as 'api.example.com' (no scheme, port or path; no IP address, no 'localhost'). A host not listed is refused, and none listed refuses every request.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HttpSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Hosts = null);
