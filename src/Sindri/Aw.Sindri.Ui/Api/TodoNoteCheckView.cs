namespace Aw.Sindri.Ui.Api;

/// <summary>
/// POST /sindri/todos/note/check (todo spec §4.4): what is in the note, and whether the disk can swap a file in place. Writes nothing. Guessed shape: the spec lists the contents (the counts, the can't-import
/// line numbers, over the cap, whether the disk can swap) and no golden exists yet.
/// </summary>
/// <param name="Exists">False when the note is not there yet (Sindri creates it on Start syncing).</param>
/// <param name="Open">The open tasks in the note (the count Import sends back as expectOpen).</param>
/// <param name="Done">The ticked tasks.</param>
/// <param name="Other">The lines that are not tasks and not blank.</param>
/// <param name="CantImport">The open tasks that can't be imported, with line numbers and reasons.</param>
/// <param name="WillImport">How many open tasks an import adopts (less than the open count minus the ones that can't be imported when the cap cuts it).</param>
/// <param name="Cap">The most open todos Sindri keeps.</param>
/// <param name="SindriOpen">The open todos Sindri already has; the import adds them after the note's lines.</param>
/// <param name="CanSwap">False when the disk has no atomic swap: the sync is one way.</param>
internal sealed record TodoNoteCheckView(bool Exists, int Open, int Done, int Other, IReadOnlyList<TodoCantSyncView> CantImport, int WillImport, int Cap, int SindriOpen, bool CanSwap);
