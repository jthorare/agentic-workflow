namespace Aw.Meta.Core.Ports;

/// <summary>One /bugFixOrchestrator candidate as its run's state.json records it, with the run's own outcome.</summary>
/// <param name="Run">The run's directory name (e.g. gh-9).</param>
/// <param name="Candidate">The candidate's id (e.g. c1).</param>
/// <param name="Worktree">The candidate's worktree directory.</param>
/// <param name="Branch">The candidate's branch.</param>
/// <param name="Commit">The commit that was evaluated.</param>
/// <param name="Evaluated">Whether the candidate was evaluated.</param>
/// <param name="Resolves">Whether it resolved the bug.</param>
/// <param name="Finished">Whether the run is over.</param>
/// <param name="ResolvedBy">The id of the candidate the run settled on, or null.</param>
public sealed record BugfixCandidate(string Run, string Candidate, string Worktree, string Branch, string Commit, bool Evaluated, bool Resolves, bool Finished, string? ResolvedBy);
