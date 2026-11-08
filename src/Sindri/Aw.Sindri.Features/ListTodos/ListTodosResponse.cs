using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListTodos;

/// <summary>The todos of one state, with the counts of every state.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="State">The state that was asked for.</param>
/// <param name="Counts">How many todos each state holds.</param>
/// <param name="Todos">The todos, oldest first.</param>
public sealed record ListTodosResponse(AsOf AsOf, TodoListState State, TodoCounts Counts, IReadOnlyList<TodoRow> Todos);
