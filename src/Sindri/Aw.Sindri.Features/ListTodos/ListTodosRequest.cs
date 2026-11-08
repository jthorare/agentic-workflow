using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.ListTodos;

/// <summary>The owner's todos (GET /sindri/todos?state=, 'sindri todos'; todo spec §4.4): request class.</summary>
/// <param name="State">Which todos: open (the default), done or all.</param>
public sealed record ListTodosRequest(TodoListState State = TodoListState.Open);
