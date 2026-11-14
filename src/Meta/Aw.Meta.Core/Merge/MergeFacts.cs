using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;

namespace Aw.Meta.Core.Merge;

/// <summary>Everything a merge decision looks at.</summary>
public sealed record MergeFacts
{
    /// <summary>The pull request.</summary>
    public required int Pr { get; init; }

    /// <summary>The latest gate run for it, if any.</summary>
    public GateRecord? Gate { get; init; }

    /// <summary>The reviewer's OK and the lane's counts from the inbox; null when the team has no reviewer lane.</summary>
    public ReviewFacts? Review { get; init; }

    /// <summary>Whether the team gates in CI (gate.mode "ci"): the gate record is a read of the pull request's required checks, so the scope, main and test-log checks don't apply.</summary>
    public bool CiGate { get; init; }

    /// <summary>In ci mode: GitHub's merge state of the pull request now, read at merge time (null when the read failed, see <see cref="LiveError"/>).</summary>
    public string? LiveMergeState { get; init; }

    /// <summary>In ci mode: GitHub's review decision of the pull request now, read at merge time (null when the read failed).</summary>
    public string? LiveReviewDecision { get; init; }

    /// <summary>In ci mode: why the live read of the merge state and review decision failed; null when it worked.</summary>
    public string? LiveError { get; init; }

    /// <summary>The pull request's head now.</summary>
    public required string CurrentHead { get; init; }

    /// <summary>The main commit now.</summary>
    public required string CurrentMain { get; init; }

    /// <summary>The tree merging the gated head onto the current main gives ("git merge-tree --write-tree"); null when not computed, or when it conflicts.</summary>
    public string? MergedTree { get; init; }

    /// <summary>Whether the pull request's base branch has a GitHub merge queue, which re-tests the merged tree itself; read only when main moved past the gate.</summary>
    public bool BaseHasMergeQueue { get; init; }

    /// <summary>Whether the pull request is a draft.</summary>
    public bool IsDraft { get; init; }

    /// <summary>Paths changed between the gate's head and the current head; null when unknown.</summary>
    public IReadOnlyList<string>? DeltaPaths { get; init; }

    /// <summary>Whether a later gate with the repo scope passed on the current head.</summary>
    public bool RepoGatePassedOnHead { get; init; }

    /// <summary>Pull requests that are open now.</summary>
    public IReadOnlyList<int> OpenPrs { get; init; } = [];

    /// <summary>Merge-order rules.</summary>
    public IReadOnlyList<MergeOrderRule> MergeOrder { get; init; } = [];

    /// <summary>Globs of documentation-only paths.</summary>
    public IReadOnlyList<string> DocsOnly { get; init; } = [];

    /// <summary>The scopes the gated diff needs (the scope planner over the gate's main...head); null when unknown.</summary>
    public IReadOnlyList<string>? PlannedScopes { get; init; }

    /// <summary>For each scope the gate vouches for, the run it came from (the newest run at this head and main that ran it); null when only one run is considered.</summary>
    public IReadOnlyDictionary<string, int>? ScopeRuns { get; init; }

    /// <summary>The failing tests each red step's log names, by step name; a step missing here, or with no names, couldn't be read.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FailingTests { get; init; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>For each red step whose log has a summary: the failures the summary counts and the failed lines that name a test. A difference is a failure with no name (a crash, a timeout).</summary>
    public IReadOnlyDictionary<string, (int Summarized, int Named)> FailureCounts { get; init; } = new Dictionary<string, (int Summarized, int Named)>(StringComparer.Ordinal);
}
