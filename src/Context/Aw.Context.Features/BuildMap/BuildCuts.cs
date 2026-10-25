using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Features.BuildMap;

/// <summary>What a build takes out of the graph it keeps: the deny globs' cut, the demote globs' cut (#1043) and, when the map rules changed, the engine accepts the new rules no longer find (#1043 ask 1), each with the undo rows for the decisions on them.</summary>
/// <param name="ProfileRepos">Every repo of the profile (their globs judge records and evidence).</param>
/// <param name="MappedRepos">The repos the map is built from (their globs judge code paths).</param>
/// <param name="Found">What this build found.</param>
/// <param name="RulesChanged">Whether the rules differ from the ones the kept map was built under.</param>
/// <param name="At">When the build runs.</param>
internal sealed record BuildCuts(IReadOnlyList<ContextRepo> ProfileRepos, IReadOnlyList<ContextRepo> MappedRepos, ExtractedMap Found, bool RulesChanged, DateTimeOffset At)
{
    /// <summary>The graph a build keeps of <paramref name="graph"/> and the undo rows for the decisions the cuts take out with it.</summary>
    /// <param name="graph">The graph to cut.</param>
    /// <param name="overrides">Every override of its map.</param>
    /// <returns>The cut graph and the undo rows.</returns>
    public (GraphSnapshot Graph, IReadOnlyList<MapOverride> Undo) Apply(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides)
    {
        var (denied, deniedUndo) = MapAccess.WithoutDenied(graph, overrides, ProfileRepos, MappedRepos, At);
        var (demoted, demotedUndo) = MapAccess.WithoutDemoted(denied, overrides, MappedRepos, At);
        var (kept, lostUndo) = MapAccess.WithoutLostEngineAccepts(demoted, overrides, Found, RulesChanged, At);
        return (kept, [.. deniedUndo, .. demotedUndo, .. lostUndo]);
    }
}
