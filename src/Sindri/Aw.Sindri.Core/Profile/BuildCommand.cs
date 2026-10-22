namespace Aw.Sindri.Core.Profile;

/// <summary>
/// One of the repo's commands the build and ship stations run in the phase worktree (line spec §7.2, LN-WF9, Appendix D): a typecheck, the unit lane, a lint. It is an argv list, never a
/// shell string; it runs with the phase worktree as its working directory.
/// </summary>
/// <param name="Name">The command line as the profile wrote it ('dotnet test'); what a finding names.</param>
/// <param name="Run">The program and its arguments (<see cref="CommandLine.TryParse"/> of <paramref name="Name"/>).</param>
public sealed record BuildCommand(string Name, IReadOnlyList<string> Run);
