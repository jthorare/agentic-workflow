using Aw.Judge.Core.Ids;

namespace Aw.Judge.Tests.Ids;

/// <summary>Holds one of each Judge id so the JSON tests can send a missing, null or invalid property.</summary>
public sealed record JudgeIdsHolder(DecisionId Decision, ToolUseId ToolUse, AgentId Agent, SessionId Session, PromptId Prompt);
