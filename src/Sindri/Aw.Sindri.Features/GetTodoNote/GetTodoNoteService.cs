using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetTodoNote;

internal sealed class GetTodoNoteService : IService<GetTodoNoteRequest, GetTodoNoteResponse>
{
    public ValueTask<Result<GetTodoNoteResponse>> Handle(GetTodoNoteRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("GetTodoNote: make GetTodoNoteTests pass");
}
