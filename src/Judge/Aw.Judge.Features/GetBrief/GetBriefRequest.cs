using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.GetBrief;

/// <summary>The GetBrief request ('judge brief get &lt;toolUseId&gt;').</summary>
/// <param name="ToolUseId">The tool call that dispatched the subagent.</param>
public sealed record GetBriefRequest(ToolUseId ToolUseId);
