namespace Aw.Sindri.Ui.Api;

/// <summary>The body of POST /sindri/todos/note/stop: the profile hash the caller last saw (null the first time).</summary>
/// <param name="IfHash">The profile hash the caller last got, or null.</param>
internal sealed record TodoNoteStopBody(string? IfHash);
