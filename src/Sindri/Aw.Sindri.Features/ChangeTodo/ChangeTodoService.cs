using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.ChangeTodo;

internal sealed class ChangeTodoService : IService<ChangeTodoRequest, TodoAccepted>
{
    public ValueTask<Result<TodoAccepted>> Handle(ChangeTodoRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ChangeTodo: make ChangeTodoTests pass");
}
