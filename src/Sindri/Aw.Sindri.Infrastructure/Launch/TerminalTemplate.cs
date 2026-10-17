namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>One row of <c lang="text">config/launcher/terminals.json</c>: a terminal <c lang="text">launch init</c> and the in-memory detection know how to drive.</summary>
/// <param name="Id">The row's id (<c lang="text">--viewer kitty</c>).</param>
/// <param name="Name">The label the app shows.</param>
/// <param name="Bin">The program to look for on the login PATH.</param>
/// <param name="TermProgram">The <c lang="text">$TERM_PROGRAM</c> values that name it.</param>
/// <param name="Argv">The exec-template argv.</param>
internal sealed record TerminalTemplate(string Id, string Name, string Bin, IReadOnlyList<string> TermProgram, IReadOnlyList<string> Argv);
