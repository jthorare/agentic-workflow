using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.KeepTodo;

/// <summary>What a pick answers (todo spec §4.4). The route serves the todo in the shape the list uses.</summary>
/// <param name="Todo">The todo after the pick.</param>
/// <param name="UndoUntil">Until when the pick can be undone (10 seconds on Sindri's clock).</param>
public sealed record KeepTodoResponse(TodoRow Todo, DateTimeOffset UndoUntil);
