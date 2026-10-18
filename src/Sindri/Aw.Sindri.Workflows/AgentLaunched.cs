using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'LaunchAgentStep' returns: the session is up (its worktree and the commit it started at), or the launch failed and what the engine does with it.</summary>
/// <param name="Worktree">The run's worktree; null when the launch failed.</param>
/// <param name="StartHead">The worktree's HEAD when the session started, which the diff signal measures from; null when it couldn't be read (the signal is then not measured) or the launch failed.</param>
/// <param name="Failure">Why it did not start, and whether that parks or fails the Step; null when it started.</param>
/// <param name="Waiting">True when the session cap was still reached at the end of the activity's slice: nothing was started or recorded, and the workflow launches again.</param>
internal sealed record AgentLaunched(string? Worktree, string? StartHead, AgentLaunchFailure? Failure, bool Waiting = false);
