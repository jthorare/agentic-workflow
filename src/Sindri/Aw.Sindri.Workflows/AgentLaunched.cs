using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'LaunchAgentStep' returns: the session is up (its worktree and the commit it started at), or the launch failed and what the engine does with it.</summary>
/// <param name="Worktree">The run's worktree; null when the launch failed.</param>
/// <param name="StartHead">The worktree's HEAD when the session started, which the diff signal measures from; null when it couldn't be read (the signal is then not measured) or the launch failed.</param>
/// <param name="Failure">Why it did not start, and whether that parks or fails the Step; null when it started.</param>
/// <param name="Model">The model the launch asked for (model-pick spec 5); null runs the agent's default. The workflow keeps it so a later Step can name "the drafter's model".</param>
/// <param name="CanEscalate">True when a stronger model is still allowed inside the pin and cap and the Step reads no 'fact:workspace' (spec 7.1).</param>
internal sealed record AgentLaunched(string? Worktree, string? StartHead, AgentLaunchFailure? Failure, string? Model = null, bool CanEscalate = false);
