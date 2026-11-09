using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.KeepTodo;

/// <summary>Pick a side of a todo changed in both places (POST /sindri/todos/{id}/keep, 'sindri todo keep'; todo spec D11, §4.4): request class, so an agent session may pick too; the event records 'by'.</summary>
/// <param name="Id">The todo.</param>
/// <param name="Side">The version to keep.</param>
/// <param name="Piped">Whether the request came with fence=1 (the CLI had no terminal on stdout): the event records 'via: pipe'.</param>
public sealed record KeepTodoRequest(TodoId Id, TodoSideName Side, bool Piped = false) : ICommand<KeepTodoResponse>;
