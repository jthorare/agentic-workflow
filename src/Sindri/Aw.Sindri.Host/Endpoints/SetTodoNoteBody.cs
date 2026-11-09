namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/todos/note/set: the vault and the note (a missing one is read as empty, so the profile rules refuse it as SND-TODO-002) and the profile hash the caller saw.</summary>
/// <param name="Vault">The vault name.</param>
/// <param name="Note">The note path inside it.</param>
/// <param name="IfHash">The live profile's hash the caller saw, or null.</param>
internal sealed record SetTodoNoteBody(string? Vault = null, string? Note = null, string? IfHash = null)
{
    /// <summary>The request.</summary>
    /// <returns>The request.</returns>
    internal Features.SetTodoNote.SetTodoNoteRequest ToRequest() => new(Vault ?? string.Empty, Note ?? string.Empty, IfHash);
}
