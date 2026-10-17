namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>One row as written; <see cref="TerminalTemplate"/> keeps what detection uses.</summary>
/// <param name="Id">The row's id.</param>
/// <param name="Name">The label.</param>
/// <param name="Bin">The program.</param>
/// <param name="TermProgram">The <c lang="text">$TERM_PROGRAM</c> values.</param>
/// <param name="Argv">The argv.</param>
internal sealed record TerminalTemplateRow(string Id, string Name, string Bin, IReadOnlyList<string>? TermProgram, IReadOnlyList<string> Argv);
