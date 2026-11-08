using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetTodoNote;

/// <summary>
/// Sets both fields of 'todo.obsidian' in one edit the way 'sindri profile set' edits a key (comments and order kept, the edited text read and checked before the atomic write), so a bad vault or note is SND-TODO-002 with the key path and nothing is written.
/// The todo writes are request class: any caller may run it, and the edit is live at once, so nothing is approved.
/// </summary>
/// <param name="writer">Plans and writes the edit.</param>
/// <param name="parser">Reads the edited text.</param>
internal sealed class SetTodoNoteService(IProfileKeyWriter writer, IProfileTextParser parser) : IService<SetTodoNoteRequest, SetTodoNoteResponse>
{
    public ValueTask<Result<SetTodoNoteResponse>> Handle(SetTodoNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(Set(request));
    }

    private Result<SetTodoNoteResponse> Set(SetTodoNoteRequest request)
    {
        var plan = writer.PlanTexts([("todo.obsidian.vault", request.Vault), ("todo.obsidian.note", request.Note)]);
        if (plan.IsError)
        {
            return plan.Error;
        }

        if (plan.Value.Changed && parser.Parse(plan.Value.Text, plan.Value.Path) is { IsError: true } parsed)
        {
            return parsed.Error;   // SND-TODO-002 with the key path, or the profile's own refusal; nothing was written
        }

        var saved = writer.Save(plan.Value);
        return saved.IsError ? saved.Error : new SetTodoNoteResponse(saved.Value, request.Vault, request.Note, plan.Value.Changed);
    }
}
