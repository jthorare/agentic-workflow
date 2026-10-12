using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.MapBriefByDispatch;

/// <summary>The MapBriefByDispatch request ('judge brief map-by-dispatch &lt;sessionId&gt; &lt;promptId&gt; &lt;subagentType&gt;').</summary>
/// <param name="SessionId">The session that dispatched the subagent.</param>
/// <param name="PromptId">The prompt that dispatched it.</param>
/// <param name="SubagentType">The subagent type.</param>
public sealed record MapBriefByDispatchRequest(SessionId SessionId, PromptId PromptId, string SubagentType);
