using Aw.Judge.Core.Ids;
using Aw.Judge.Features.ApproveDecision;

namespace Aw.Judge.Testing;

/// <summary>Builds a ApproveDecisionRequest with deterministic defaults.</summary>
public sealed class ApproveDecisionRequestBuilder
{
    private DecisionId _decisionId = DecisionId.From("00000000-0000-4000-8000-000000000001");

    /// <summary>Sets DecisionId.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public ApproveDecisionRequestBuilder WithDecisionId(DecisionId value)
    {
        _decisionId = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ApproveDecisionRequest Build() => new(_decisionId);
}
