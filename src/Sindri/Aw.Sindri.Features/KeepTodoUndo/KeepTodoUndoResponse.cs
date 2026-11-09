using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.KeepTodoUndo;

/// <summary>What Undo answers: the todo with its conflict open again. The route serves it in the shape the list uses.</summary>
/// <param name="Todo">The todo.</param>
public sealed record KeepTodoUndoResponse(TodoRow Todo);
