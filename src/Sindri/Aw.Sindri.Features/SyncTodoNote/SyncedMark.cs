using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>The last 'todo.note.synced' and where it sits in the ledger.</summary>
/// <param name="Payload">The event's payload.</param>
/// <param name="Sequence">Its sequence.</param>
internal sealed record SyncedMark(TodoNoteSynced Payload, long Sequence);
