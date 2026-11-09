namespace Aw.Sindri.Features.GetTodoNote;

/// <summary>One held change of an open hold (todo spec §4.4).</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Kind">'removed', 'edited' or 'reopened'.</param>
/// <param name="Text">Sindri's text for the todo; empty when the todo is gone.</param>
/// <param name="NoteLine">The note's line; null for a removal.</param>
public sealed record TodoNoteHeldChange(Aw.Sindri.Core.Ids.TodoId Id, string Kind, string Text, string? NoteLine);
