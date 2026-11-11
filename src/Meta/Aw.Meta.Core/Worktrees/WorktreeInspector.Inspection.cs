using System.Globalization;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Worktrees;

/// <summary>The inspector's per-inspection state, in its own file to keep the inspector under the file-length limit.</summary>
public sealed partial class WorktreeInspector
{
    // What one inspection shares between its worktrees.
    private sealed class Inspection(TeamConfig team, MetaPaths paths, string? repoRoot, List<PrBranch> pulls, IReadOnlyList<ProcessLocation> scan, DateTimeOffset? since, CancellationToken cancellation)
    {
        internal DateTimeOffset? Since { get; } = since;

        internal WorktreeScope Scope { get; init; } = WorktreeScope.All;

        internal CancellationToken Cancellation { get; } = cancellation;

        internal TeamConfig Team { get; } = team;

        internal List<PrBranch> Pulls { get; } = pulls;

        internal IReadOnlyList<ProcessLocation> Scan { get; } = scan;

        // The merged pull requests that closed each GitHub issue a bugfix run names, asked once per inspection.
        internal Dictionary<int, IReadOnlyList<int>> Closers { get; } = [];

        internal IReadOnlyList<string> Roots { get; } = team.Worktrees.EffectiveRoots(team.Repo, paths.GatesDirectory, paths.Home, repoRoot);

        private static bool IsGateTreeName(string name) => string.Equals(name, "tree", StringComparison.Ordinal) || string.Equals(name, "main-tree", StringComparison.Ordinal);

        // gates/<pr>/tree and gates/<pr>/main-tree belong to that pull request's gate.
        internal int? GatePr(string path)
        {
            var prefix = WorktreeRoots.Normalize(paths.GatesDirectory) + "/";
            var normalized = WorktreeRoots.Normalize(path);
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            var parts = normalized[prefix.Length..].Split('/');
            return parts.Length == 2 && IsGateTreeName(parts[1]) && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pr) ? pr : null;
        }
    }
}
