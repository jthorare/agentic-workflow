using Aw.Judge.Core.Ids;
using Aw.Judge.Features.SaveBrief;

namespace Aw.Judge.Testing;

/// <summary>Builds a SaveBriefRequest with deterministic defaults: no teammate name, a general-purpose subagent.</summary>
public sealed class SaveBriefRequestBuilder
{
    private SaveBriefRequest _request = new(
        ToolUseId.From("toolu_1"), SessionId.From("session-1"), PromptId.From("prompt-1"),
        DispatchName: null, SubagentType: "general-purpose", Goal: "Fix the failing test",
        AcceptanceCriteria: "The test passes", ProofCommand: "npm test");

    /// <summary>Sets ToolUseId.</summary>
    /// <param name="value">The id text.</param>
    /// <returns>This builder.</returns>
    public SaveBriefRequestBuilder WithToolUseId(string value)
    {
        _request = _request with { ToolUseId = ToolUseId.From(value) };
        return this;
    }

    /// <summary>Sets the goal, the acceptance criteria and the proof command.</summary>
    /// <param name="goal">The goal.</param>
    /// <param name="acceptanceCriteria">The acceptance criteria.</param>
    /// <param name="proofCommand">The proof command.</param>
    /// <returns>This builder.</returns>
    public SaveBriefRequestBuilder WithText(string goal, string acceptanceCriteria, string proofCommand)
    {
        _request = _request with { Goal = goal, AcceptanceCriteria = acceptanceCriteria, ProofCommand = proofCommand };
        return this;
    }

    /// <summary>Sets the teammate name.</summary>
    /// <param name="value">The name.</param>
    /// <returns>This builder.</returns>
    public SaveBriefRequestBuilder Named(string value)
    {
        _request = _request with { DispatchName = value };
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SaveBriefRequest Build() => _request;
}
