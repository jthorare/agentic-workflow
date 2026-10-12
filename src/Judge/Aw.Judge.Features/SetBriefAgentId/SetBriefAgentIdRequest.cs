using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.SetBriefAgentId;

/// <summary>The SetBriefAgentId request ('judge brief set-agent-id &lt;toolUseId&gt; &lt;agentId&gt;').</summary>
/// <param name="ToolUseId">The tool call that dispatched the subagent.</param>
/// <param name="AgentId">The agent that started for it.</param>
public sealed record SetBriefAgentIdRequest(ToolUseId ToolUseId, AgentId AgentId);
