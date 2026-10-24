namespace Aw.Sindri.Features.LineDoctor;

/// <summary>Check that a repo is set up for the line (line spec Appendix D): a read that writes nothing and starts nothing.</summary>
/// <param name="Directory">The repo's checkout, or a directory inside it.</param>
public sealed record LineDoctorRequest(string Directory);
