using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListTodos;

internal sealed class ListTodosService : IService<ListTodosRequest, ListTodosResponse>
{
    public ValueTask<Result<ListTodosResponse>> Handle(ListTodosRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ListTodos: make ListTodosTests pass");
}
