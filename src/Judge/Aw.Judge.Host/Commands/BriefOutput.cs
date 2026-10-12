using Aw.Judge.Core.Briefs;

namespace Aw.Judge.Host.Commands;

/// <summary>What 'judge brief get' prints: the brief with the hook contract's camelCase field names.</summary>
internal sealed record BriefOutput(
    string ToolUseId,
    string? AgentId,
    string SessionId,
    string PromptId,
    string? DispatchName,
    string SubagentType,
    string Goal,
    string AcceptanceCriteria,
    string ProofCommand,
    DateTimeOffset SavedAt)
{
    internal static BriefOutput From(Brief brief) => new(
        brief.ToolUseId.Value, brief.AgentId?.Value, brief.SessionId.Value, brief.PromptId.Value, brief.DispatchName,
        brief.SubagentType, brief.Goal, brief.AcceptanceCriteria, brief.ProofCommand, brief.SavedAt);
}
