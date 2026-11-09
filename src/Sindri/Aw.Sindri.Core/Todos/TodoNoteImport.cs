using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Core.Todos;

/// <summary>What an import found and did (todo spec §4.4 'todo note import', REQ-E6).</summary>
/// <param name="Counts">The note's counts as the import re-read them.</param>
/// <param name="Imported">How many open lines were adopted; 0 for a dry run.</param>
/// <param name="DryRun">Whether nothing was written.</param>
public sealed record TodoNoteImport(NoteCounts Counts, int Imported, bool DryRun);
