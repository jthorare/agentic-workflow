using Aw.Judge.Core.Errors;
using Aw.Judge.Features.SaveBrief;
using Aw.Kernel;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// The 'brief save' stdin record (hook contract: scope-gate.sh writes these field names). Every field is required and a
/// string, empty allowed; dispatchName may be null; other keys are ignored. The ids are plain strings here and become
/// typed ids in <see cref="ToRequest"/>.
/// </summary>
internal sealed record BriefInput(
    string ToolUseId,
    string SessionId,
    string PromptId,
    string? DispatchName,
    string SubagentType,
    string Goal,
    string AcceptanceCriteria,
    string ProofCommand)
{
    /// <summary>The SaveBrief request, or AW-JUDGE-001 naming the id that is past its cap.</summary>
    internal Result<SaveBriefRequest> ToRequest()
    {
        if (!Core.Ids.ToolUseId.TryFrom(ToolUseId, out var toolUseId))
        {
            return Invalid("toolUseId");
        }

        if (!Core.Ids.SessionId.TryFrom(SessionId, out var sessionId))
        {
            return Invalid("sessionId");
        }

        return Core.Ids.PromptId.TryFrom(PromptId, out var promptId)
            ? new SaveBriefRequest(toolUseId, sessionId, promptId, DispatchName, SubagentType, Goal, AcceptanceCriteria, ProofCommand)
            : Invalid("promptId");
    }

    private static Result<SaveBriefRequest> Invalid(string field) => JudgeErrors.Invalid.With($"{field} is not a valid id");
}
