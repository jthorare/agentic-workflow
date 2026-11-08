using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetTodo;

/// <summary>One todo with its open conflict (todo spec §4.4): a read of its row; SND-TODO-404 when there is none.</summary>
/// <param name="views">The todos.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class GetTodoService(IViewReader views, IProjectorStatus status, ICallerContextAccessor callers) : IService<GetTodoRequest, GetTodoResponse>
{
    public async ValueTask<Result<GetTodoResponse>> Handle(GetTodoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("a todo needs a caller");
        }

        var row = await views.Get<TodoRow>(ViewNames.Todo, request.Id.Value, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        return row.Value is { } found ? new GetTodoResponse(status.Current.AsOf, found) : SindriErrors.TodoNotFound.With($"No todo {request.Id.Value}.");
    }
}
