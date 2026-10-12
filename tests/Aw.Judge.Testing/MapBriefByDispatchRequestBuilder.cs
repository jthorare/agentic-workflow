using Aw.Judge.Core.Ids;
using Aw.Judge.Features.MapBriefByDispatch;

namespace Aw.Judge.Testing;

/// <summary>Builds a MapBriefByDispatchRequest with deterministic defaults: the default brief's dispatch.</summary>
public sealed class MapBriefByDispatchRequestBuilder
{
    private MapBriefByDispatchRequest _request = new(SessionId.From("session-1"), PromptId.From("prompt-1"), "general-purpose");

    /// <summary>Sets the subagent type.</summary>
    /// <param name="value">The type.</param>
    /// <returns>This builder.</returns>
    public MapBriefByDispatchRequestBuilder OfType(string value)
    {
        _request = _request with { SubagentType = value };
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public MapBriefByDispatchRequest Build() => _request;
}
