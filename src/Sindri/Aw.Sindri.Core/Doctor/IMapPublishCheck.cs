using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Doctor;

/// <summary>The doctor's 'context:publish' row, read by the context-calls slice for the run-doctor slice.</summary>
public interface IMapPublishCheck
{
    /// <summary>The row for the approved profile's repos.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The row.</returns>
    public ValueTask<DoctorCheck> Check(LoadedProfile? approved, CancellationToken cancellationToken);
}
