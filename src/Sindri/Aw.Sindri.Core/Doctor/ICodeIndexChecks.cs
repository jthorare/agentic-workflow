using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Doctor;

/// <summary>The code index's doctor rows (code index spec §8), read from the index's stored state; the doctor slice calls it and the code index slice answers.</summary>
public interface ICodeIndexChecks
{
    /// <summary>The checks, in the order doctor prints them.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One row per repo, then graphify, ollama and the lock.</returns>
    public ValueTask<DoctorCheck[]> Check(LoadedProfile? approved, CancellationToken cancellationToken);
}
