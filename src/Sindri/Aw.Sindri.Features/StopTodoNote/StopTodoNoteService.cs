using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.StopTodoNote;

/// <summary>Stub: the red commit of the 'todo note stop' slice.</summary>
internal sealed class StopTodoNoteService : IService<StopTodoNoteRequest, StopTodoNoteResponse>
{
    public ValueTask<Result<StopTodoNoteResponse>> Handle(StopTodoNoteRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
