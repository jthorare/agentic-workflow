using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetTodo;

internal sealed class GetTodoService : IService<GetTodoRequest, GetTodoResponse>
{
    public ValueTask<Result<GetTodoResponse>> Handle(GetTodoRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("GetTodo: make GetTodoTests pass");
}
