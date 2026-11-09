namespace Aw.Sindri.Ui.Api;

/// <summary>An open hold on a mass change from the note (todo spec §3.1, §4.5, REQ-E7).</summary>
/// <param name="NoteHash">The note the hold was found against; Apply and Put them back send it back.</param>
/// <param name="HeldAt">When the hold opened.</param>
/// <param name="OpenInBase">The base's open todos (the 10% rule).</param>
/// <param name="Changes">Every held change.</param>
internal sealed record TodoHoldView(string NoteHash, DateTimeOffset HeldAt, int OpenInBase, IReadOnlyList<TodoHoldChangeView> Changes);
