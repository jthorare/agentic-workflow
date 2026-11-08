
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>A known id sits on a line that breaks D3: nothing changes on either side and the sync line says why.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="LineNumber">The line.</param>
/// <param name="Reason">The words of <see cref="NoteBreakReasons"/>.</param>
public sealed record NoteLineBlocked(TodoId Id, int LineNumber, string Reason) : NoteIntent(Id);
