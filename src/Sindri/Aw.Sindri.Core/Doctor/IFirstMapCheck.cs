using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Doctor;

/// <summary>The doctor's 'context:map' row while no map exists (#3216), read by the context-calls slice for the run-doctor slice: the context engine can't see the jobs that build the first map.</summary>
public interface IFirstMapCheck
{
    /// <summary>The row for the approved profile's repos.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The row.</returns>
    public ValueTask<DoctorCheck> Check(LoadedProfile? approved, CancellationToken cancellationToken);
}
