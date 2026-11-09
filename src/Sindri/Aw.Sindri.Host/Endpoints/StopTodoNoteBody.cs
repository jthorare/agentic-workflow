namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/todos/note/stop: the profile hash the caller saw.</summary>
/// <param name="IfHash">The live profile's hash the caller saw, or null.</param>
internal sealed record StopTodoNoteBody(string? IfHash = null)
{
    /// <summary>The request.</summary>
    /// <returns>The request.</returns>
    internal Features.StopTodoNote.StopTodoNoteRequest ToRequest() => new(IfHash);
}
