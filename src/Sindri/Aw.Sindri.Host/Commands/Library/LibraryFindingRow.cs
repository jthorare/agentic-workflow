namespace Aw.Sindri.Host.Commands.Library;

/// <summary>One finding of 'sindri library validate'.</summary>
/// <param name="Rule">The SND-LIB code.</param>
/// <param name="File">The file.</param>
/// <param name="Line">The line; null for a whole-file finding.</param>
/// <param name="Message">What is wrong.</param>
/// <param name="Fix">What to change.</param>
internal sealed record LibraryFindingRow(string Rule, string File, int? Line, string Message, string Fix);
