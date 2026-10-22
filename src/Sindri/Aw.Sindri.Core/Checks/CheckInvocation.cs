namespace Aw.Sindri.Core.Checks;

/// <summary>One command of the repo (a typecheck, the unit lane, the red tests) ready to run: a program and its arguments, never a shell string.</summary>
/// <param name="Name">What a finding calls it ('typecheck').</param>
/// <param name="Argv">The program, then its arguments; at least the program.</param>
/// <param name="Timeout">The longest it may run before it is killed.</param>
public sealed record CheckInvocation(string Name, IReadOnlyList<string> Argv, TimeSpan Timeout);
