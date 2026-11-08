using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.StopTodoNote;

/// <summary>Removes 'todo.obsidian' from the live profile with the key writer's removal (comments and order kept, the edited text checked before the atomic write). A key that is already absent is "unchanged". Request class, live at once.</summary>
/// <param name="writer">Plans and writes the edit.</param>
/// <param name="parser">Reads the edited text.</param>
internal sealed class StopTodoNoteService(IProfileKeyWriter writer, IProfileTextParser parser) : IService<StopTodoNoteRequest, StopTodoNoteResponse>
{
    private const string Key = "todo.obsidian";

    public ValueTask<Result<StopTodoNoteResponse>> Handle(StopTodoNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(Stop());
    }

    private Result<StopTodoNoteResponse> Stop()
    {
        var plan = writer.PlanRemove(Key);
        if (plan.IsError)
        {
            return plan.Error;
        }

        if (plan.Value.Changed && parser.Parse(plan.Value.Text, plan.Value.Path) is { IsError: true } parsed)
        {
            return parsed.Error;
        }

        var saved = writer.Save(plan.Value);
        return saved.IsError ? saved.Error : new StopTodoNoteResponse(saved.Value, plan.Value.Changed ? [Key] : [], plan.Value.Changed);
    }
}
