using Aw.Kernel;
using Aw.Sindri.Features.KeepTodoUndo;
using Aw.Sindri.Features.ListTodos;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>What POST /sindri/todos/{id}/keep/undo answers: the todo with its conflict open again.</summary>
/// <param name="Todo">The todo.</param>
internal sealed record KeepTodoUndoWire(TodoItem Todo)
{
    /// <summary>Maps the service's answer.</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The wire form, or the same error.</returns>
    internal static Result<KeepTodoUndoWire> From(Result<KeepTodoUndoResponse> answer)
    {
        if (answer.IsError)
        {
            return answer.Error;
        }

        return new KeepTodoUndoWire(TodoItem.From(answer.Value.Todo));
    }
}
