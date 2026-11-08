namespace Aw.Sindri.Features.TodoNoteProfile;

/// <summary>The answer of POST /sindri/todos/note/set and /stop (todo spec §4.4).</summary>
/// <param name="Changed">False when the profile already had that state and the file was left alone.</param>
/// <param name="ProfileHash">The hash of the live profile after the write, for the next write's ifHash.</param>
public sealed record TodoNoteWritten(bool Changed, string ProfileHash);
