using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>One change the note made that a hold is holding, as 'todo.note.held' carries it (todo spec §4.1).</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Kind">'removed', 'edited' or 'reopened'.</param>
/// <param name="NoteLine">The note's line for it; null for 'removed'.</param>
public sealed record TodoHeldChange(TodoId Id, string Kind, string? NoteLine);
