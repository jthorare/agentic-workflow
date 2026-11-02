using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'LaunchAgentStep' returns: the session is up (its worktree and the commit it started at), or the launch failed and what the engine does with it.</summary>
/// <param name="Worktree">The run's worktree; null when the launch failed.</param>
/// <param name="StartHead">The worktree's HEAD when the session started, which the diff signal measures from; null when it couldn't be read (the signal is then not measured) or the launch failed.</param>
/// <param name="Failure">Why it did not start, and whether that parks or fails the Step; null when it started.</param>
/// <param name="Model">The model the launch asked for (model-pick spec 5); null runs the agent's default. The workflow keeps it so a later Step can name "the drafter's model".</param>
/// <param name="CanEscalate">True when a stronger model is still allowed inside the pin and cap and the Step reads no 'fact:workspace' (spec 7.1).</param>
/// <param name="Waiting">True when the session cap was still reached at the end of the activity's slice: nothing was started or recorded, and the workflow launches again.</param>
/// <param name="Agent">The launcher agent the session runs on (#1269); the workflow copies it, with <paramref name="Model"/>, onto the Step's completion. Null when the launch failed and in a history recorded before the field.</param>
/// <param name="RunDirectory">The Step's run directory the launcher made (StartedLaunch.RunDirectory, copied, never recomputed); recovery reads its outputs there. Null for a run launched before the field and for a launch with none: recovery then reads the worktree.</param>
internal sealed record AgentLaunched(string? Worktree, string? StartHead, AgentLaunchFailure? Failure, string? Model = null, bool CanEscalate = false, bool Waiting = false, string? Agent = null, string? RunDirectory = null);
