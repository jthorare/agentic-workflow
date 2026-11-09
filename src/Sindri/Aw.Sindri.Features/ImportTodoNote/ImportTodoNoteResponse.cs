using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.ImportTodoNote;

/// <summary>What an import found and did (todo spec §4.4).</summary>
/// <param name="Imported">How many lines were adopted; 0 for a dry run.</param>
/// <param name="CantImport">How many open lines break the todo text rules and stay as text.</param>
/// <param name="Open">Open task lines, those that can't import included.</param>
/// <param name="Done">Done task lines.</param>
/// <param name="Other">Lines that aren't top-level task lines.</param>
/// <param name="Importable">Open lines an import adopts: the number to pass as 'expectOpen'.</param>
/// <param name="CantImportLines">Every open task line that breaks the todo text rules, with its line number and reason.</param>
/// <param name="DryRun">Whether nothing was written.</param>
public sealed record ImportTodoNoteResponse(int Imported, int CantImport, int Open, int Done, int Other, int Importable, IReadOnlyList<NoteCantImport> CantImportLines, bool DryRun);
