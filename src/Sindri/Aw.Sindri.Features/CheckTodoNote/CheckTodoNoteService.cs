using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CheckTodoNote;

/// <summary>Stub: the red commit of the 'note/check' slice.</summary>
internal sealed class CheckTodoNoteService : IService<CheckTodoNoteRequest, CheckTodoNoteResponse>
{
    public ValueTask<Result<CheckTodoNoteResponse>> Handle(CheckTodoNoteRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
