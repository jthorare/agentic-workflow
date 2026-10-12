using Aw.Judge.Core.Ids;
using Aw.Judge.Features.SetBriefAgentId;

namespace Aw.Judge.Testing;

/// <summary>Builds a SetBriefAgentIdRequest with deterministic defaults.</summary>
public sealed class SetBriefAgentIdRequestBuilder
{
    private ToolUseId _toolUseId = ToolUseId.From("toolu_1");
    private AgentId _agentId = AgentId.From("agentid-1");

    /// <summary>Sets ToolUseId.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public SetBriefAgentIdRequestBuilder WithToolUseId(ToolUseId value)
    {
        _toolUseId = value;
        return this;
    }

    /// <summary>Sets AgentId.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public SetBriefAgentIdRequestBuilder WithAgentId(AgentId value)
    {
        _agentId = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SetBriefAgentIdRequest Build() => new(_toolUseId, _agentId);
}
