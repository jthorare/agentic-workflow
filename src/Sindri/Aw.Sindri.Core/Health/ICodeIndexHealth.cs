using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Health;

/// <summary>The code index's rows for GET /status's health: one problem per repo whose index is stale, damaged, retrying a build or with a failed code graph. A port: the code-index reads implement it, the status read only asks.</summary>
public interface ICodeIndexHealth
{
    /// <summary>The problems across the approved profile's repos.</summary>
    /// <param name="approved">The approved profile, or null before the first approval.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The problems; none when nothing is approved or the index can't be read (status never fails over the index).</returns>
    public ValueTask<IReadOnlyList<HealthProblem>> Problems(LoadedProfile? approved, CancellationToken cancellationToken);
}
