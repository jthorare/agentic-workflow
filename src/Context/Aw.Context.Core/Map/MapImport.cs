using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Roles;

namespace Aw.Context.Core.Map;

/// <summary>A checked map file, ready to write: keys normalized and migrated, decision nodes and 'quotes' edges added.</summary>
/// <param name="Repo">The repo it maps.</param>
/// <param name="Commit">Its commit, or null.</param>
/// <param name="Graph">The graph.</param>
/// <param name="Overrides">The overrides it carries.</param>
/// <param name="Heads">Where each mapped repo stood when it was built (a build names them all); null for a version that copies another's (a replaced draft keeps its heads) or an import, which keeps the one commit it names.</param>
public sealed record MapImport(ContextRepoName Repo, string? Commit, GraphSnapshot Graph, IReadOnlyList<MapOverride> Overrides, IReadOnlyList<MapHead>? Heads = null)
{
    /// <summary>The hash of the map rules it was built under (<see cref="MapRuleset"/>), or null when none is recorded (an import, a promote that keeps the draft's own).</summary>
    public string? Ruleset { get; init; }

    /// <summary>The tracked files each ignored role dropped (<see cref="MapVersion.Dropped"/>), or null when none is recorded.</summary>
    public IReadOnlyList<RoleDropped>? Dropped { get; init; }

    /// <summary>Its counts.</summary>
    public MapCounts Counts => MapCounts.Of(Graph, Overrides.Count);
}
