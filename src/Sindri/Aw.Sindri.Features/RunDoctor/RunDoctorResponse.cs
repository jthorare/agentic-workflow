using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>One line per check, and the exit code they add up to.</summary>
/// <param name="Checks">The checks, in a fixed order: lease, ledger, profile, api.</param>
/// <param name="ExitCode">0 all ok, 1 any warn, 2 any fail.</param>
public sealed record RunDoctorResponse(IReadOnlyList<DoctorCheck> Checks, int ExitCode);
