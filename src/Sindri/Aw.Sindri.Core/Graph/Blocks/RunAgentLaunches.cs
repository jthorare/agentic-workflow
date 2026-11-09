using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The pure half of a 'run_agent' node's launch (PR 7): the worktree, the sandbox host groups and the unattended request the engine hands to the launcher.</summary>
public static class RunAgentLaunches
{
    /// <summary>The worktree the node works in: '&lt;repo&gt;/.claude/worktrees/wf-&lt;slug&gt;-&lt;short run&gt;'.</summary>
    /// <param name="address">The node run.</param>
    /// <param name="repoPath">The repo checkout.</param>
    /// <returns>The path.</returns>
    public static string WorktreeFor(NodeRunAddress address, string repoPath) => throw new NotSupportedException("red");

    /// <summary>The sandbox host groups a node adds: 'github' when it names a repo, then the groups of the connections it names that <paramref name="connectionGroups"/> knows.</summary>
    /// <param name="config">The node's config.</param>
    /// <param name="connectionGroups">The host groups of a connection by name; PR 10 supplies them, and until then none.</param>
    /// <returns>The group names, each once.</returns>
    public static IReadOnlyList<string> GroupsFor(RunAgentConfig config, Func<string, IReadOnlyList<string>> connectionGroups) => throw new NotSupportedException("red");

    /// <summary>The launcher's request: unattended, Open, a fresh worktree, the node's address and task, and a policy that lets the session report, write its worktree and commit it.</summary>
    /// <param name="address">The node run.</param>
    /// <param name="target">Where it runs: the item, the agent and the repo checkout.</param>
    /// <param name="config">The node's config.</param>
    /// <param name="brief">The first message.</param>
    /// <param name="groups">The sandbox host groups the node adds (<see cref="GroupsFor"/>).</param>
    /// <returns>The request.</returns>
    public static LaunchRequest RequestFor(NodeRunAddress address, AgentStepTarget target, RunAgentConfig config, string brief, IReadOnlyList<string> groups) => throw new NotSupportedException("red");
}
