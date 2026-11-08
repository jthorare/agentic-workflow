using Aw.Kernel;
using Aw.Sindri.Ui.Api.Todos;

namespace Aw.Sindri.Ui.Api;

/// <summary>The todo calls (<see cref="SindriApiClient"/>, continued; todo spec §4.4): the list and the six writes.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<TodoListView>> Todos(Uri api, string state, CancellationToken cancellationToken) =>
        throw new NotSupportedException(TodoRoutes.List(state));

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> AddTodo(Uri api, TodoAdd add, CancellationToken cancellationToken) =>
        throw new NotSupportedException(TodoRoutes.Add);

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> ChangeTodo(Uri api, string id, TodoChange change, CancellationToken cancellationToken) =>
        throw new NotSupportedException(TodoRoutes.Change(id));

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> DoneTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        throw new NotSupportedException(TodoRoutes.Done(id));

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> ReopenTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        throw new NotSupportedException(TodoRoutes.Reopen(id));

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> RemoveTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        throw new NotSupportedException(TodoRoutes.Remove(id));

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> RestoreTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        throw new NotSupportedException(TodoRoutes.Restore(id));
}
