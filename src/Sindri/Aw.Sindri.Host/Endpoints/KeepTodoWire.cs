using Aw.Kernel;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.KeepTodo;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>What POST /sindri/todos/{id}/keep answers: the todo in the shape the list serves, and until when the pick can be undone.</summary>
/// <param name="Todo">The todo after the pick.</param>
/// <param name="UndoUntil">The end of the 10 s Undo window.</param>
internal sealed record KeepTodoWire(TodoItem Todo, DateTimeOffset UndoUntil)
{
    /// <summary>Maps the service's answer.</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The wire form, or the same error.</returns>
    internal static Result<KeepTodoWire> From(Result<KeepTodoResponse> answer)
    {
        if (answer.IsError)
        {
            return answer.Error;
        }

        return new KeepTodoWire(TodoItem.From(answer.Value.Todo), answer.Value.UndoUntil);
    }
}
