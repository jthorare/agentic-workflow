using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetTodo;

/// <summary>One todo, with its open conflict when it has one, in the same wire shape the list serves.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Todo">The todo.</param>
public sealed record GetTodoResponse(AsOf AsOf, TodoItem Todo);
