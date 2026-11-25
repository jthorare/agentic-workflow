using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Worktrees;

/// <summary>The inspector's reading of the bugfix runs' records (#2005 --discarded, #2753 finished runs), in its own file to keep the inspector under the file-length limit.</summary>
public sealed partial class WorktreeInspector
{
    // Every sweep reads the bugfix rows naming the tree: a run whose GitHub issue a merged pull request closed finished its trees
    // (FinishedBugfix). Only --discarded needs the rows to judge, so only there a failed read keeps the tree unjudged; anywhere
    // else it just leaves FinishedBugfix unset (unknown is never finished).
    private async ValueTask<Result<WorktreeFacts>> WithBugfix(Inspection inspection, WorktreeEntry entry, WorktreeFacts composed)
    {
        var rows = await bugfix.Naming(entry.Path, inspection.Cancellation).ConfigureAwait(false);
        if (rows.IsError)
        {
            return inspection.Scope.Discarded ? rows.Error : composed;
        }

        var finished = await FinishedBugfix(inspection, entry, rows.Value).ConfigureAwait(false);
        if (!inspection.Scope.Discarded)
        {
            return composed with { FinishedBugfix = finished };
        }

        List<BugfixCandidate> candidates = [.. rows.Value.Where(static r => !r.Baseline)];
        return composed with
        {
            FinishedBugfix = finished,
            Discarded = DiscardedCandidates.Match(entry, candidates),
            NotDiscarded = finished is null ? DiscardedCandidates.WhyNot(entry, candidates) : null,
        };
    }

    // "bugfix gh-9 red tree, #9 closed by merged #12" when a row names the tree at its HEAD (a candidate also on its branch) and the
    // run's issue is closed by a merged pull request; null otherwise.
    private async ValueTask<string?> FinishedBugfix(Inspection inspection, WorktreeEntry entry, IReadOnlyList<BugfixCandidate> rows)
    {
        foreach (var row in rows)
        {
            var merged = IssueAtHead(row, entry) is { } issue ? await MergedClosers(inspection, issue).ConfigureAwait(false) : [];
            if (merged.Count > 0)
            {
                var what = row.Baseline ? "red tree" : "candidate " + row.Candidate;
                return string.Create(CultureInfo.InvariantCulture, $"bugfix {row.Run} {what}, #{row.Issue} closed by merged #{merged[0]}");
            }
        }

        return null;
    }

    // A --pr sweep also takes the trees of the bugfix runs whose issue one of these pull requests closed, so merge-run frees them in the same step.
    private async ValueTask<bool> ClosedByOneOf(Inspection inspection, WorktreeEntry entry, IReadOnlySet<int> prs)
    {
        var rows = await bugfix.Naming(entry.Path, inspection.Cancellation).ConfigureAwait(false);
        if (rows.IsError)
        {
            return false;
        }

        foreach (var row in rows.Value)
        {
            var merged = IssueAtHead(row, entry) is { } issue ? await MergedClosers(inspection, issue).ConfigureAwait(false) : [];
            if (merged.Any(prs.Contains))
            {
                return true;
            }
        }

        return false;
    }

    // The run's GitHub issue when the row names this tree at its HEAD (a candidate also on its branch; the red tree's branch isn't recorded); null otherwise.
    private static int? IssueAtHead(BugfixCandidate row, WorktreeEntry entry) =>
        row.Commit.Length > 0 && string.Equals(row.Commit, entry.Head, StringComparison.Ordinal)
        && (row.Baseline || string.Equals(row.Branch, entry.Branch, StringComparison.Ordinal))
            ? row.Issue
            : null;

    // The merged pull requests that closed the issue, asked once per issue per inspection; a gh failure is none.
    private async ValueTask<IReadOnlyList<int>> MergedClosers(Inspection inspection, int issue)
    {
        if (inspection.Closers.TryGetValue(issue, out var known))
        {
            return known;
        }

        var json = await repo.Gh(inspection.Team.Repo, "api", "graphql", "-F", "owner={owner}", "-F", "name={repo}", "-F", "number=" + issue.ToString(CultureInfo.InvariantCulture), "-f", "query=" + ClosingPullRequests.Query).ConfigureAwait(false);
        var merged = json.IsSuccess ? ClosingPullRequests.Merged(json.Value) : [];
        inspection.Closers[issue] = merged;
        return merged;
    }
}
