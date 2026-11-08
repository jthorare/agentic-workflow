using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetTodoNote;

/// <summary>The Obsidian todo note's state (todo spec §4.4). Ph1a answers 'not set'; the sync (Ph3b) fills it.</summary>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class GetTodoNoteService(IProjectorStatus status, ICallerContextAccessor callers) : IService<GetTodoNoteRequest, GetTodoNoteResponse>
{
    public ValueTask<Result<GetTodoNoteResponse>> Handle(GetTodoNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<GetTodoNoteResponse>>(callers.Current is null
            ? SindriErrors.AuthRefused.With("the todo note needs a caller")
            : new GetTodoNoteResponse(status.Current.AsOf, "not set"));
    }
}
