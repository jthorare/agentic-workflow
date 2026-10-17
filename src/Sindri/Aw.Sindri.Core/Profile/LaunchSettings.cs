using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's 'launch:' block (UI spec E-4, launcher L-14). Left out of the canonical JSON when absent, so a profile
/// without it keeps the hash it was approved under.
/// </summary>
/// <param name="Default">The default launch target; an agent id checked by pattern only (the catalog is per box).</param>
/// <param name="Repos">Per-repo overrides by repo name ('launch.repos.&lt;repo&gt;.default').</param>
/// <param name="ConfirmWindow">How long a started launch asks for confirmation before another (a duration; default 2h, <see cref="DefaultConfirmWindow"/>).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LaunchSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AgentId? Default = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, LaunchRepoSettings>? Repos = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ConfirmWindow = null)
{
    /// <summary>The confirm window when the profile sets none.</summary>
    public static TimeSpan DefaultConfirmWindow { get; } = TimeSpan.FromHours(2);
}
