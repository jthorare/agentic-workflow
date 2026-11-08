using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The todo note's setup calls of the API (<see cref="ISindriApi"/>, continued; todo spec §3.4 Settings card, §4.4): check a note, import it, start syncing it, stop.</summary>
internal partial interface ISindriApi
{
    /// <summary>POST /sindri/todos/note/check (todo spec §4.4): reads the note and says what an import would do. Writes nothing.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="vault">The vault's name.</param>
    /// <param name="note">The note's vault-relative path.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The counts; SND-TODO-002 (a bad vault or note, with the key path); or SND-UI-002/003.</returns>
    public Task<Result<TodoNoteCheckView>> CheckTodoNote(Uri api, string vault, string note, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/note/import (todo spec §4.4): adds ids to the note's open tasks and adds Sindri's todos after them. The profile's key must name that note.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="vault">The vault's name.</param>
    /// <param name="note">The note's vault-relative path.</param>
    /// <param name="expectOpen">The open count Check note showed.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What was adopted; SND-TODO-409 (the open count is no longer expectOpen); SND-TODO-002, -008; or SND-UI-002/003.</returns>
    public Task<Result<TodoNoteImportedView>> ImportTodoNote(Uri api, string vault, string note, int expectOpen, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/note/set (todo spec §4.4, §4.5): writes todo.obsidian to the live profile (live at once, no approval).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="vault">The vault's name.</param>
    /// <param name="note">The note's vault-relative path.</param>
    /// <param name="ifHash">The profile hash the last set or stop answered, or null; a profile that moved on since is SND-TODO-409.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether the profile changed, and its hash; SND-TODO-002, SND-TODO-409 or SND-PROFILE-001/002; or SND-UI-002/003.</returns>
    public Task<Result<TodoNoteChangedView>> SetTodoNote(Uri api, string vault, string note, string? ifHash, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/note/stop (todo spec §4.4): removes todo.obsidian from the live profile; the note stays as it is.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="ifHash">The profile hash the last set or stop answered, or null; a profile that moved on since is SND-TODO-409.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether the profile changed, and its hash; SND-TODO-409 or SND-PROFILE-001/002; or SND-UI-002/003.</returns>
    public Task<Result<TodoNoteChangedView>> StopTodoNote(Uri api, string? ifHash, CancellationToken cancellationToken);
}
