using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetTodo;

/// <summary>One todo (GET /sindri/todos/{id}, 'sindri todo show'; todo spec §4.4): request class.</summary>
/// <param name="Id">The todo.</param>
public sealed record GetTodoRequest(TodoId Id);
