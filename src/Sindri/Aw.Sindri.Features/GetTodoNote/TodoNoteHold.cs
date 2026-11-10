namespace Aw.Sindri.Features.GetTodoNote;

/// <summary>The open hold on a mass change from the note (todo spec §4.4, REQ-E7).</summary>
/// <param name="NoteHash">The note the hold was drawn from; Apply and Put them back send it back.</param>
/// <param name="HeldAt">When the hold opened (or was reopened by an Undo).</param>
/// <param name="OpenInBase">The open todos the 10% rule counted against.</param>
/// <param name="Changes">Every held change.</param>
public sealed record TodoNoteHold(string NoteHash, DateTimeOffset HeldAt, int OpenInBase, IReadOnlyList<TodoNoteHeldChange> Changes);
