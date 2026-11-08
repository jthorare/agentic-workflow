namespace Aw.Sindri.Ui.Api;

/// <summary>The answer of the note set and the note stop (todo spec §4.4, "unchanged" when the profile already had the value). The shape of the changed flag is a guess; the profile hash came from the control plane's side.</summary>
/// <param name="Changed">False when the profile was already as asked.</param>
/// <param name="ProfileHash">The live profile's hash after the call; the next set or stop sends it back as ifHash. Null from a Sindri that does not send it.</param>
internal sealed record TodoNoteChangedView(bool Changed, string? ProfileHash = null);
