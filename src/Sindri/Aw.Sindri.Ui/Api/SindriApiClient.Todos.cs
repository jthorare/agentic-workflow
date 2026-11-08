using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Todos;

namespace Aw.Sindri.Ui.Api;

/// <summary>The todo calls (<see cref="SindriApiClient"/>, continued; todo spec §4.4): the list and the six writes.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<TodoListView>> Todos(Uri api, string state, CancellationToken cancellationToken) =>
        Get<TodoListView>(api, TodoRoutes.List(state), cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> AddTodo(Uri api, TodoAdd add, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Add, AwJson.Serialize(add, Json), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> ChangeTodo(Uri api, string id, TodoChange change, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Change(id), AwJson.Serialize(change, Json), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> DoneTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Done(id), AwJson.Serialize(new TodoVersion(version), Json), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> ReopenTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Reopen(id), AwJson.Serialize(new TodoVersion(version), Json), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> RemoveTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Remove(id), AwJson.Serialize(new TodoVersion(version), Json), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoAcceptedView>> RestoreTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Restore(id), AwJson.Serialize(new TodoVersion(version), Json), cancellationToken);

    private Task<Result<TodoAcceptedView>> TodoWrite(Uri api, string route, byte[] body, CancellationToken cancellationToken) =>
        Call<TodoAcceptedView>(api, HttpMethod.Post, route, cancellationToken, new CallOptions(Content: body));
}
