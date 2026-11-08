using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.AddTodo;

internal sealed class AddTodoService : IService<AddTodoRequest, TodoAccepted>
{
    public ValueTask<Result<TodoAccepted>> Handle(AddTodoRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("AddTodo: make AddTodoTests pass");
}
