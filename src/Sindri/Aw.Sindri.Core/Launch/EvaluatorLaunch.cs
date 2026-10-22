namespace Aw.Sindri.Core.Launch;

/// <summary>
/// What makes a launch the holdout evaluator's (line spec §9, LN-LCH2): the launcher checks out <paramref name="HeadSha"/> into a scratch checkout of its own,
/// starts the session there instead of the phase worktree, and removes the checkout when the launch ends.
/// </summary>
/// <param name="HeadSha">The commit to check out: the head the hidden scenarios run against; 7 to 64 hex digits.</param>
/// <param name="HoldoutPath">The phase's directory in the holdout store, mounted read-only for the session.</param>
/// <param name="PhaseWorktree">The phase's worktree, which the session can neither read nor write.</param>
public sealed record EvaluatorLaunch(string HeadSha, string HoldoutPath, string PhaseWorktree);
