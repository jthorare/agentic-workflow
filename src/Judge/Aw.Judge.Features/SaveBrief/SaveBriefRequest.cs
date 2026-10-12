using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.SaveBrief;

/// <summary>The SaveBrief request: the brief the scope gate saves for a subagent dispatch ('judge brief save').</summary>
/// <param name="ToolUseId">The tool call that dispatched the subagent.</param>
/// <param name="SessionId">The session that dispatched it.</param>
/// <param name="PromptId">The prompt that dispatched it.</param>
/// <param name="DispatchName">The teammate name, when it had one.</param>
/// <param name="SubagentType">The subagent type.</param>
/// <param name="Goal">What the subagent is for.</param>
/// <param name="AcceptanceCriteria">How its result is judged done.</param>
/// <param name="ProofCommand">The command that proves it.</param>
public sealed record SaveBriefRequest(
    ToolUseId ToolUseId,
    SessionId SessionId,
    PromptId PromptId,
    string? DispatchName,
    string SubagentType,
    string Goal,
    string AcceptanceCriteria,
    string ProofCommand);
