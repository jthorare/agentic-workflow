namespace Aw.Sindri.Core.Sandbox;

/// <summary>Reads a repo's learn-mode history from the ledger (sandbox spec §3.6, Q13), so the plan builder can close the window.</summary>
public interface ILearnHistory
{
    /// <summary>Reads the history.</summary>
    /// <param name="repo">The repo's name as the profile keys it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The history; an empty one when the repo never ran in learn mode.</returns>
    public ValueTask<LearnHistory> Read(string repo, CancellationToken cancellationToken);
}
