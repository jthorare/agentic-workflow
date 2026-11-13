namespace Aw.Sindri.Core.Launch;

/// <summary>Port: the two facts a one-off start checks before it makes anything (agents spec §15.4). Checks only; they start nothing.</summary>
public interface IStartChecks
{
    /// <summary>Whether <paramref name="agent"/> is installed and signed in for the owner.</summary>
    /// <param name="agent">The catalog entry.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The facts.</returns>
    public ValueTask<AgentReadiness> Agent(AgentEntry agent, CancellationToken cancellationToken);

    /// <summary>Whether the folder exists. 'tmux new-session -c' on a missing folder starts the agent in the server's own directory, so a start checks first (SND-AGENT-012).</summary>
    /// <param name="path">The repo's folder.</param>
    /// <returns>True when it exists.</returns>
    public bool FolderExists(string path);
}
