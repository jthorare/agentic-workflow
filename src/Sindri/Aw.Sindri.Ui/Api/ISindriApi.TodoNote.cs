using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The todo note reads and picks of the API (<see cref="ISindriApi"/>, continued; todo spec §4.4): the sync's state, the conflict picks and the hold picks.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/todos/note (todo spec §4.4): the note's state, open conflicts, hold and picks. A view read. An older Sindri answers a bare 404 (OlderSindri.IsNotServed).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The note's state, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<TodoNoteView>> TodoNote(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/{id}/keep (todo spec §4.4, D11): pick a version of a both-places conflict.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The todo's id.</param>
    /// <param name="side">"sindri" or "obsidian".</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted pick; SND-TODO-404/005/409; or SND-UI-002/003.</returns>
    public Task<Result<TodoKeepAcceptedView>> KeepTodo(Uri api, string id, string side, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/{id}/keep/undo (todo spec §4.4): reopen the conflict while the pick's 10 s are open.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The todo's id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted undo; SND-TODO-404/005/409; or SND-UI-002/003.</returns>
    public Task<Result<TodoKeepUndoneView>> UndoKeepTodo(Uri api, string id, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/note/hold/apply (todo spec §4.4): apply every held change in Sindri.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="noteHash">The hold's noteHash the row was drawn from.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted pick; SND-TODO-005 (no hold open), -409 (the note changed since); or SND-UI-002/003.</returns>
    public Task<Result<TodoHoldPickedView>> ApplyHold(Uri api, string noteHash, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/note/hold/restore (todo spec §4.4): put the held lines back in the note.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="noteHash">The hold's noteHash the row was drawn from.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted pick; SND-TODO-005, -409; or SND-UI-002/003.</returns>
    public Task<Result<TodoHoldPickedView>> RestoreHold(Uri api, string noteHash, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/note/hold/undo (todo spec §4.4): reopen the hold while the pick's 10 s are open.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted undo; SND-TODO-005 (nothing to undo), -409; or SND-UI-002/003.</returns>
    public Task<Result<TodoHoldUndoneView>> UndoHold(Uri api, CancellationToken cancellationToken);
}
