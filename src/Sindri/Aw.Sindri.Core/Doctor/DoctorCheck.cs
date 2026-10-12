namespace Aw.Sindri.Core.Doctor;

/// <summary>One doctor line: the check, its verdict, what it found and the command that fixes it.</summary>
/// <param name="Name">The check.</param>
/// <param name="State">The verdict.</param>
/// <param name="Detail">What it found.</param>
/// <param name="Fix">The fix command, when not ok.</param>
public sealed record DoctorCheck(string Name, CheckState State, string Detail, string? Fix);
