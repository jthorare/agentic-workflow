namespace Aw.Sindri.Ui.Api;

/// <summary>The answer of the note set and the note stop (todo spec §4.4, "unchanged" when the profile already had the value). Guessed shape.</summary>
/// <param name="Changed">False when the profile was already as asked.</param>
internal sealed record TodoNoteChangedView(bool Changed);
