using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.LineDoctor;

/// <summary>What 'line doctor' found.</summary>
/// <param name="Repo">The repo's name in the approved profile; null when the directory is in none.</param>
/// <param name="Checks">One row per check: ok, warn or fail, what was found and the fix.</param>
public sealed record LineDoctorResponse(string? Repo, IReadOnlyList<DoctorCheck> Checks)
{
    /// <summary>Whether no row is warn or fail.</summary>
    public bool Passed => Checks.All(static c => c.State == CheckState.Ok);
}
