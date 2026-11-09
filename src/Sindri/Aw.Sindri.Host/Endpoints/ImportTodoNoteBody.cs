using Aw.Sindri.Features.ImportTodoNote;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/todos/note/import (todo spec §4.4): the vault and the note the caller saw, and the importable count they saw; no count is a dry run. A missing vault or note reads as empty, so the service refuses it as SND-TODO-002.</summary>
/// <param name="Vault">The vault name.</param>
/// <param name="Note">The note path inside it.</param>
/// <param name="ExpectOpen">The importable open count; null for a dry run.</param>
/// <param name="DryRun">True to count and write nothing even when a count is given.</param>
internal sealed record ImportTodoNoteBody(string? Vault = null, string? Note = null, int? ExpectOpen = null, bool DryRun = false)
{
    /// <summary>The request.</summary>
    /// <param name="piped">Whether the request came with fence=1.</param>
    /// <returns>The request.</returns>
    internal ImportTodoNoteRequest ToRequest(bool piped) => new(Vault ?? string.Empty, Note ?? string.Empty, DryRun ? null : ExpectOpen, piped);
}
