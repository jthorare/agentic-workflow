using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.AddTodo;

/// <summary>Add a todo (POST /sindri/todos, 'sindri todo add'; todo spec §4.4): request class, appends 'todo.added'.</summary>
/// <param name="Text">The one-line text (1 to 280 characters, todo spec D3).</param>
/// <param name="Due">The due date, or null.</param>
/// <param name="Links">The typed links (at most 10), or null.</param>
/// <param name="ClientId">The caller's retry id: a retried add with the same one makes one todo and counts once, or null.</param>
/// <param name="Piped">Whether the request came with fence=1 (the CLI had no terminal on stdout): the event records 'via: pipe' and the hourly limit counts it.</param>
public sealed record AddTodoRequest(string Text, DateOnly? Due = null, IReadOnlyList<string>? Links = null, TodoClientId? ClientId = null, bool Piped = false) : ICommand<TodoAccepted>;
