using System.Text.Json.Serialization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Roles;

namespace Aw.Context.Core.Map;

/// <summary>What 'context map status' shows.</summary>
/// <param name="Version">The current version.</param>
/// <param name="Counts">Its counts (a session's: only the nodes its repo implements, the records and decisions linked to them (owner decision 2026-10-15, B) and what lies between them).</param>
/// <param name="Orphaned">Overrides whose target no longer resolves.</param>
/// <param name="Retained">How many earlier versions are kept.</param>
/// <param name="ScopedTo">The repo the counts are scoped to, or null for the whole map.</param>
/// <param name="Coverage">With an anchor: the coverage of its own scope, the same record 'context lenses' serves; left out otherwise.</param>
public sealed record MapStatusView(
    MapVersion Version,
    MapCounts Counts,
    int Orphaned,
    int Retained,
    ContextRepoName? ScopedTo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LensCoverage? Coverage = null)
{
    /// <summary>True when the current version records the map rules it was built under and they differ from the ones the scope runs under now (issue 1041); false, and left out of the JSON, otherwise or when none were recorded.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RulesOlder { get; init; }

    /// <summary>Per repo, the indexed files each configured role matches and the role-like files no glob covers (issue 1105); left out when no repo in scope configures a role or has such a file.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RepoRoles>? Roles { get; init; }
}
