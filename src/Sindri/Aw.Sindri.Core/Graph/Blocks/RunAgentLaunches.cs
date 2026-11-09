using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The pure half of a 'run_agent' node's launch (PR 7): the worktree, the sandbox host groups and the unattended request the engine hands to the launcher.</summary>
public static class RunAgentLaunches
{
    private static readonly string[] _report = ["Bash(sindri workflow complete:*)", "Bash(sindri workflow status:*)", "Bash(sindri workflow events:*)"];
    private static readonly string[] _commit = ["Bash(git add:*)", "Bash(git commit:*)", "Bash(git status:*)", "Bash(git diff:*)"];

    /// <summary>The worktree the node works in: '&lt;repo&gt;/.claude/worktrees/wf-&lt;slug&gt;-&lt;short run&gt;'.</summary>
    /// <param name="address">The node run.</param>
    /// <param name="repoPath">The repo checkout.</param>
    /// <returns>The path.</returns>
    public static string WorktreeFor(NodeRunAddress address, string repoPath)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(repoPath);
        return repoPath.TrimEnd('/') + "/.claude/worktrees/wf-" + address.Workflow.Value + "-" + address.ShortRun;
    }

    /// <summary>The sandbox host groups a node adds: 'github' when it names a repo, then the groups of the connections it names that <paramref name="connectionGroups"/> knows.</summary>
    /// <param name="config">The node's config.</param>
    /// <param name="connectionGroups">The host groups of a connection by name; PR 10 supplies them, and until then none.</param>
    /// <returns>The group names, each once.</returns>
    public static IReadOnlyList<string> GroupsFor(RunAgentConfig config, Func<string, IReadOnlyList<string>> connectionGroups)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(connectionGroups);
        List<string> groups = [];
        if (config.Repo.Length > 0)
        {
            groups.Add("github");
        }

        foreach (var group in config.Connections.SelectMany(connectionGroups))
        {
            if (!groups.Contains(group, StringComparer.Ordinal))
            {
                groups.Add(group);
            }
        }

        return groups;
    }

    /// <summary>The launcher's request: unattended, Open, a fresh worktree, the node's address and task, and a policy that lets the session report, write its worktree and commit it.</summary>
    /// <param name="address">The node run.</param>
    /// <param name="target">Where it runs: the item, the agent and the repo checkout.</param>
    /// <param name="config">The node's config.</param>
    /// <param name="brief">The first message.</param>
    /// <param name="groups">The sandbox host groups the node adds (<see cref="GroupsFor"/>).</param>
    /// <returns>The request.</returns>
    public static LaunchRequest RequestFor(NodeRunAddress address, AgentStepTarget target, RunAgentConfig config, string brief, IReadOnlyList<string> groups)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(config);
        var worktree = WorktreeFor(address, target.RepoPath);
        var agent = config.Agent is { } named && AgentId.TryFrom(named, out var id) ? id : target.Agent;
        // Never plan mode: plan mode runs read-only tools only, so the session couldn't run 'sindri workflow complete'. The node may report, write its own worktree and commit it, nothing else.
        var policy = new StepPolicy([.. _report, .. _commit], StepPermissionMode.Default, worktree);
        var step = new StepArgs(Model: null, policy, new StepRef(address.Node.Value, "run_agent"), brief);
        return new LaunchRequest(address.Launch, target.Item, agent, LaunchMode.Open, Attended: false, target.RepoPath, Instance: null, worktree, step, Run: null)
        {
            Node = address,
            Task = new AgentTask(brief, config.Outputs, groups),
            StepTimeout = config.Wall ?? config.Timeout,
        };
    }

    /// <summary>The same request with checkouts the session may read and never write (the node's 'readOnlyRepos').</summary>
    /// <param name="request">A request <see cref="RequestFor"/> made.</param>
    /// <param name="paths">The checkouts.</param>
    /// <returns>The request; <paramref name="request"/> itself when there are no paths.</returns>
    public static LaunchRequest ReadingOnly(LaunchRequest request, IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(paths);
        return paths.Count == 0 || request.Step?.Policy is not { } policy ? request : request with { Step = request.Step with { Policy = policy with { ReadOnlyPaths = paths } } };
    }
}
