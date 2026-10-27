namespace Aw.Sindri.Core.Launches;

/// <summary>The agent runs idle past <see cref="LaunchRules.IdleAfter"/> and still not ended (issue #1000), for health and Needs you.</summary>
public interface IStalledAgentRuns
{
    /// <summary>The stalled runs now.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The runs, in no set order.</returns>
    public ValueTask<IReadOnlyList<StalledAgentRun>> Find(CancellationToken cancellationToken);
}
