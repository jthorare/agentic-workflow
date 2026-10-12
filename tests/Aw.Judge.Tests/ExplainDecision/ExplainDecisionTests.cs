using Aw.Judge.Core.Errors;
using Aw.Judge.Features.ExplainDecision;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.ExplainDecision;

public sealed class ExplainDecisionTests
{
    private readonly InMemoryDecisionLog _decisions = new();

    [Fact, Trait("Lane", "unit")]
    public async Task ExplainDecision_returns_the_recorded_decision()
    {
        var decision = A.Decision().Build();
        _ = await _decisions.Record(decision, CancellationToken.None);

        var result = await new ExplainDecisionService(_decisions).Handle(A.ExplainDecisionRequest().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(decision.Id, result.Value.Decision.Id);
        Assert.Equal(decision.ReasonCode, result.Value.Decision.ReasonCode);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ExplainDecision_returns_UnknownDecision_for_an_unrecorded_id()
    {
        var result = await new ExplainDecisionService(_decisions).Handle(A.ExplainDecisionRequest().Build(), CancellationToken.None);

        Assert.Equal(JudgeErrors.UnknownDecision.Code, result.Error.Code);
    }
}
