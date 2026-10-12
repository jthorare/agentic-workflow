using Aw.Judge.Core.Errors;
using Aw.Judge.Features.UndoDecision;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.UndoDecision;

public sealed class UndoDecisionTests
{
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch.AddHours(1));
    private readonly InMemoryDecisionLog _decisions = new();

    [Fact, Trait("Lane", "unit")]
    public async Task UndoDecision_marks_the_decision_undone_at_the_clock_time()
    {
        _ = await _decisions.Record(A.Decision().Build(), CancellationToken.None);

        var result = await new UndoDecisionService(_decisions, _clock).Handle(A.UndoDecisionRequest().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_clock.GetUtcNow(), result.Value.Decision.UndoneAt);
        Assert.Equal(_clock.GetUtcNow(), Assert.Single(_decisions.Decisions).UndoneAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task UndoDecision_returns_UnknownDecision_for_an_unrecorded_id()
    {
        var result = await new UndoDecisionService(_decisions, _clock).Handle(A.UndoDecisionRequest().Build(), CancellationToken.None);

        Assert.Equal(JudgeErrors.UnknownDecision.Code, result.Error.Code);
    }
}
