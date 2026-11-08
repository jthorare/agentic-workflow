using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.SetTodoNote;

/// <summary>Stub: the red commit of the 'todo note set' slice.</summary>
internal sealed class SetTodoNoteService : IService<SetTodoNoteRequest, SetTodoNoteResponse>
{
    public ValueTask<Result<SetTodoNoteResponse>> Handle(SetTodoNoteRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
