namespace Aw.Sindri.Ui.Api;

/// <summary>The body of the hold picks (todo spec §4.4): the note the row was drawn from, so a note that changed since refuses the pick.</summary>
/// <param name="NoteHash">The hold's noteHash.</param>
internal sealed record TodoHoldBody(string NoteHash);
