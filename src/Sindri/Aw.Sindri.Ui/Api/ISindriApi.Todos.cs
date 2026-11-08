using Aw.Kernel;
using Aw.Sindri.Ui.Api.Todos;

namespace Aw.Sindri.Ui.Api;

/// <summary>The todo reads and writes of the API (<see cref="ISindriApi"/>, continued; todo spec §4.4).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/todos?state=open|done|all (todo spec §4.4): the todos and the counts. A view read: it waits on the views.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="state">"open", "done" or "all".</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<TodoListView>> Todos(Uri api, string state, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos (todo spec §4.4, request class): add a todo.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="add">The text, the day, the links and the client id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted write carrying the new id and the day it read; SND-TODO-001/003/004/006/007/429; or SND-UI-002/003.</returns>
    public Task<Result<TodoAcceptedView>> AddTodo(Uri api, TodoAdd add, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/{id}/change (todo spec §4.4): edit text, day or links.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The todo's id.</param>
    /// <param name="change">The version the row showed and what to change.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted write; SND-TODO-409 (changed since), -423 (changed in both places), -404; or SND-UI-002/003.</returns>
    public Task<Result<TodoAcceptedView>> ChangeTodo(Uri api, string id, TodoChange change, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/{id}/done (todo spec §4.4): check a todo off.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The todo's id.</param>
    /// <param name="version">The version the row showed, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted write; SND-TODO-404/409/423; or SND-UI-002/003.</returns>
    public Task<Result<TodoAcceptedView>> DoneTodo(Uri api, string id, long? version, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/{id}/reopen (todo spec §4.4): put a done todo back.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The todo's id.</param>
    /// <param name="version">The version the row showed, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted write; SND-TODO-404/409/423; or SND-UI-002/003.</returns>
    public Task<Result<TodoAcceptedView>> ReopenTodo(Uri api, string id, long? version, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/{id}/remove (todo spec §4.4): remove a todo (it stays restorable).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The todo's id.</param>
    /// <param name="version">The version the row showed, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted write; SND-TODO-404/409/423; or SND-UI-002/003.</returns>
    public Task<Result<TodoAcceptedView>> RemoveTodo(Uri api, string id, long? version, CancellationToken cancellationToken);

    /// <summary>POST /sindri/todos/{id}/restore (todo spec §4.4): bring a removed todo back.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The todo's id.</param>
    /// <param name="version">The version the row showed, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted write; SND-TODO-404/409/423; or SND-UI-002/003.</returns>
    public Task<Result<TodoAcceptedView>> RestoreTodo(Uri api, string id, long? version, CancellationToken cancellationToken);
}
