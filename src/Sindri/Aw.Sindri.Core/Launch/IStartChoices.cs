namespace Aw.Sindri.Core.Launch;

/// <summary>Port: the repo and agent of the last one-off start, kept in a small file under the Sindri home so a start never rewrites the profile (agents spec §15.4).</summary>
public interface IStartChoices
{
    /// <summary>Records the choice of a start that returned 2xx. Best effort: a failed write is not a failed start.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="agent">The agent id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the file is written.</returns>
    public ValueTask Remember(string repo, string agent, CancellationToken cancellationToken);

    /// <summary>The choice the last 2xx start recorded.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The choice; both members null when nothing was recorded or the file can't be read.</returns>
    public ValueTask<StartChoice> Last(CancellationToken cancellationToken);
}
