using Aw.Context.Core.Engine;

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
}
