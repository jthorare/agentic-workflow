using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Health;

/// <summary>
/// The context sources' rows for GET /status's health (#4010): one problem per source the server flags as needing a look (its pull failed or backs
/// off, or a pass ended and found nothing). A port: the context reads implement it, the status read only asks, so the pill, the System headline and
/// the Context index card say the same thing.
/// </summary>
public interface IContextSourceHealth
{
    /// <summary>The problems across the profile's context sources.</summary>
    /// <param name="approved">The approved profile, or null before the first approval.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The problems; none when nothing is approved or the sources can't be read (status never fails over the context index).</returns>
    public ValueTask<IReadOnlyList<HealthProblem>> Problems(LoadedProfile? approved, CancellationToken cancellationToken);
}
