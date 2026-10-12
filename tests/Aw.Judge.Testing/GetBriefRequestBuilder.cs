using Aw.Judge.Core.Ids;
using Aw.Judge.Features.GetBrief;

namespace Aw.Judge.Testing;

/// <summary>Builds a GetBriefRequest with deterministic defaults: the default brief's tool use id.</summary>
public sealed class GetBriefRequestBuilder
{
    private ToolUseId _toolUseId = ToolUseId.From("toolu_1");

    /// <summary>Sets ToolUseId.</summary>
    /// <param name="value">The id text.</param>
    /// <returns>This builder.</returns>
    public GetBriefRequestBuilder WithToolUseId(string value)
    {
        _toolUseId = ToolUseId.From(value);
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetBriefRequest Build() => new(_toolUseId);
}
