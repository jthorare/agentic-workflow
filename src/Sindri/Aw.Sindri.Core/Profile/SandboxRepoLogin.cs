using System.Text.Json.Serialization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'sandbox.repos.&lt;repo&gt;.login' override.</summary>
/// <param name="Source">The source for this repo; the most specific value wins (repo, then target, then profile).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SandboxRepoLogin(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ModelLoginSource? Source = null);
