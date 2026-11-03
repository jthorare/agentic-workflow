using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Worktrees;

/// <summary>Reads a worktree against the bugfix runs' records: is it a losing candidate at its HEAD, and if not, why not.</summary>
internal static class DiscardedCandidates
{
    // The loser's description when a row names this tree at this branch and commit as evaluated, not resolving, with the run over or another candidate chosen.
    internal static string? Match(WorktreeEntry entry, IReadOnlyList<BugfixCandidate> rows)
    {
        var loser = rows.FirstOrDefault(r => Same(r.Worktree, entry.Path) && r.Branch == entry.Branch && r.Commit == entry.Head && r.Evaluated && !r.Resolves
            && (r.Finished || (r.ResolvedBy is { } by && by != r.Candidate)));
        return loser is null ? null : $"bugfix {loser.Run} candidate {loser.Candidate}, " + (loser.ResolvedBy is { } winner ? "resolved by " + winner : "run finished");
    }

    // The first rule that fails, in the order the owner reads them; null when the tree is a discarded candidate.
    internal static string? WhyNot(WorktreeEntry entry, IReadOnlyList<BugfixCandidate> rows)
    {
        if (Match(entry, rows) is not null)
        {
            return null;
        }

        var named = rows.Where(r => Same(r.Worktree, entry.Path)).ToList();
        if (named.Count == 0)
        {
            return "no bugfix run names this worktree";
        }

        var here = named.Where(r => r.Branch == entry.Branch && r.Commit == entry.Head).ToList();
        if (here.Count == 0)
        {
            return "HEAD moved past the evaluated commit";
        }

        if (here.Find(static r => r.Resolves) is { } winner)
        {
            return $"candidate {winner.Candidate} resolved the bug";
        }

        return here.Find(static r => !r.Evaluated) is { } unevaluated ? $"candidate {unevaluated.Candidate} was never evaluated" : "the bugfix run is not over";
    }

    private static bool Same(string a, string b) => WorktreeRoots.Normalize(a) == WorktreeRoots.Normalize(b);
}
