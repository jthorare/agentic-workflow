using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Ids;
using Aw.Kernel;

namespace Aw.Judge.Core.Ports;

/// <summary>Where dispatch briefs are kept. Every call returns an error, never throws, when the store can't be used.</summary>
public interface IBriefStore
{
    /// <summary>Saves a new brief.</summary>
    /// <param name="brief">The brief.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or AW-JUDGE-004 when a brief for the tool use id is already saved.</returns>
    public ValueTask<Result<Unit>> Save(Brief brief, CancellationToken cancellationToken);

    /// <summary>The brief a tool call saved.</summary>
    /// <param name="toolUseId">The tool use id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The brief, or null when there is none.</returns>
    public ValueTask<Result<Brief?>> ByToolUseId(ToolUseId toolUseId, CancellationToken cancellationToken);

    /// <summary>The oldest brief dispatched under a teammate name that isn't mapped to an agent yet.</summary>
    /// <param name="dispatchName">The teammate name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The brief, or null when there is none.</returns>
    public ValueTask<Result<Brief?>> FirstUnmappedByDispatchName(string dispatchName, CancellationToken cancellationToken);

    /// <summary>The oldest unmapped brief a session's prompt dispatched to a subagent type.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="promptId">The prompt.</param>
    /// <param name="subagentType">The subagent type.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The brief, or null when there is none.</returns>
    public ValueTask<Result<Brief?>> FirstUnmappedByDispatch(SessionId sessionId, PromptId promptId, string subagentType, CancellationToken cancellationToken);

    /// <summary>Maps a brief to the agent that started for it; an unknown tool use id changes nothing.</summary>
    /// <param name="toolUseId">The tool use id.</param>
    /// <param name="agentId">The agent id.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the store's error.</returns>
    public ValueTask<Result<Unit>> SetAgentId(ToolUseId toolUseId, AgentId agentId, CancellationToken cancellationToken);
}
