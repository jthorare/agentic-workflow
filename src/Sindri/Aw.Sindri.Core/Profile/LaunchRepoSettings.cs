using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Profile;

/// <summary>One repo's 'launch.repos.&lt;repo&gt;' entry (UI spec E-4).</summary>
/// <param name="Default">The repo's default launch target; checked by the id pattern only, never against the catalog (launcher L-14).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LaunchRepoSettings([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AgentId? Default = null);
