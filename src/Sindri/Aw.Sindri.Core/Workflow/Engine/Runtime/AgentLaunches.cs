using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The pure half of an agent Step's launch (engine P8 D4, D8): its deterministic launch id and the unattended request the engine hands to the launcher.</summary>
public static class AgentLaunches
{
    /// <summary>The launch id of a run: 'LCH-' and the first 32 hex digits of SHA-256 of the instance id, ':' and the run token. A retry or a replay can never start a second session.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The id.</returns>
    public static LaunchId LaunchIdFor(AgentStepRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(run.Workflow.Value + ":" + run.Run.Value));
        return LaunchId.From("LCH-" + Convert.ToHexStringLower(digest)[..32]);
    }

    /// <summary>The worktree the launcher creates for the run: '&lt;repo&gt;/.claude/worktrees/sindri-&lt;instance&gt;-&lt;run&gt;'.</summary>
    /// <param name="run">The run.</param>
    /// <param name="target">Where it runs.</param>
    /// <returns>The path.</returns>
    public static string WorktreeFor(AgentStepRun run, AgentStepTarget target)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(target);
        return target.RepoPath.TrimEnd('/') + "/.claude/worktrees/sindri-" + run.Workflow.Value + "-" + run.Run.Value;
    }

    /// <summary>The launcher's request: unattended, Open, the worktree, the Step's reference and the policy its capability gives.</summary>
    /// <param name="run">The run.</param>
    /// <param name="target">Where it runs.</param>
    /// <returns>The request.</returns>
    public static LaunchRequest RequestFor(AgentStepRun run, AgentStepTarget target)
    {
        var worktree = WorktreeFor(run, target);
        var policy = run.WriteScope
            ? new StepPolicy([], StepPermissionMode.Default, worktree, run.LoginSource)   // a Step only tightens the profile: it may write its own worktree and nothing else
            : new StepPolicy([], StepPermissionMode.ReadOnly, WritablePath: null, run.LoginSource);
        return new LaunchRequest(
            LaunchIdFor(run), target.Item, target.Agent, LaunchMode.Open, Attended: false, target.RepoPath, run.Address, worktree,
            new StepArgs(Model: null, policy, run.Step), run.Run);   // Model null: per-Step models are SC-R
    }
}
