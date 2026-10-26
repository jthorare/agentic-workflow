using System.Text.Json.Serialization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Roles;

namespace Aw.Context.Core.Map;

/// <summary>One map version (table map_version).</summary>
/// <param name="Number">Its number (shown as "map v13"), assigned by the store in order.</param>
/// <param name="Repo">The repo it maps.</param>
/// <param name="Commit">The commit it was built at, or null for an imported map that names none.</param>
/// <param name="KeyVersion">The EntryKey version of its keys.</param>
/// <param name="Created">When it was written.</param>
/// <param name="Status">Its lifecycle state.</param>
// The number is a plain int, not a Vogen id: a Vogen int id makes PublicApiAnalyzers report RS0041 on its generated members.
public sealed record MapVersion(int Number, ContextRepoName Repo, string? Commit, int KeyVersion, DateTimeOffset Created, MapVersionStatus Status)
{
    /// <summary>The hash of the map rules it was built under, or null when none was recorded (a version built before issue 1041, or imported).</summary>
    public string? Ruleset { get; init; }

    /// <summary>The tracked files each ignored role dropped when it was built (issue 1161); null when none was recorded (a version built before, or imported), empty when no role was ignored.</summary>
    [JsonIgnore]   // why: served through the roles of map status (RoleCount.Ignored); the version objects in the API goldens stay as they were
    public IReadOnlyList<RoleDropped>? Dropped { get; init; }
}
