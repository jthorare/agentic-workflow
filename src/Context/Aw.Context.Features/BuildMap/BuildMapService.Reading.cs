using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Roles;

namespace Aw.Context.Features.BuildMap;

internal sealed partial class BuildMapService
{
    // What the build read: the merged map, and where each repo stood as this build read it (kept with the draft for the publish rules).
    private sealed record Reading(ExtractedMap Found, IReadOnlyList<string> Degraded, ContextRepoName Name, string Commit, IReadOnlyList<MapHead> Heads, ContextSettings Settings)
    {
        public IReadOnlyList<RoleDropped> Dropped { get; init; } = [];

        public IReadOnlyList<string> RecordNotes { get; init; } = [];

        // The tokens labeling used across the repos, which the product-surface step shares.
        public int Spent { get; init; }
    }

    private sealed record RepoExtraction(ExtractedMap Map, string Commit, IReadOnlyList<string> Degraded, IReadOnlyList<RoleDropped> Dropped, IReadOnlyList<string> Paths, IReadOnlyDictionary<string, NodeKey> Directories)
    {
        public int Spent { get; init; }
    }
}
