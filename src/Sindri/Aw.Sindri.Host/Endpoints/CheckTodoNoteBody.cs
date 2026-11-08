using Aw.Sindri.Features.CheckTodoNote;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/todos/note/check (todo spec §4.4): the vault and the note, with a missing one read as empty so the service refuses it as SND-TODO-002.</summary>
/// <param name="Vault">The vault name.</param>
/// <param name="Note">The note path inside it.</param>
internal sealed record CheckTodoNoteBody(string? Vault = null, string? Note = null)
{
    /// <summary>The request.</summary>
    /// <returns>The request.</returns>
    internal CheckTodoNoteRequest ToRequest() => new(Vault ?? string.Empty, Note ?? string.Empty);
}
