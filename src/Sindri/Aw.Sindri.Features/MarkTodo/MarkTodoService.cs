using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.MarkTodo;

internal sealed class MarkTodoService : IService<MarkTodoRequest, TodoAccepted>
{
    public ValueTask<Result<TodoAccepted>> Handle(MarkTodoRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("MarkTodo: make MarkTodoTests pass");
}
