namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'RecoverAgentNode' is given (PR 7): the node run and where its session wrote.</summary>
/// <param name="Node">The node run.</param>
/// <param name="Worktree">The launch's worktree: where outputs are read from when <paramref name="RunDirectory"/> is null.</param>
/// <param name="RunDirectory">The node run's directory (AgentLaunched.RunDirectory); the output folder is .sindri-out under it.</param>
internal sealed record AgentNodeRecover(AgentNodeRef Node, string Worktree, string? RunDirectory)
{
    /// <summary>Where the output folder is: the run directory, else the worktree.</summary>
    public string Root => RunDirectory ?? Worktree;
}
