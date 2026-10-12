using Aw.Judge.Core.Errors;
using Aw.Judge.Features.ApproveDecision;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.ApproveDecision;

public sealed class ApproveDecisionTests
{
    private readonly InMemoryDecisionLog _decisions = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch.AddHours(1));

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveDecision_records_the_approved_time_and_keeps_the_reason_code()
    {
        _ = await _decisions.Record(A.Decision().Build(), CancellationToken.None);

        var result = await Approve();

        Assert.True(result.IsSuccess);
        Assert.Equal(KnownInstants.Epoch.AddHours(1), result.Value.Decision.ApprovedAt);
        Assert.Equal("model", result.Value.Decision.ReasonCode);
        Assert.Equal(KnownInstants.Epoch.AddHours(1), Assert.Single(_decisions.Decisions).ApprovedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveDecision_twice_keeps_the_first_approved_time()
    {
        _ = await _decisions.Record(A.Decision().Build(), CancellationToken.None);
        _ = await Approve();
        _clock.Advance(TimeSpan.FromMinutes(10));

        var again = await Approve();

        Assert.True(again.IsSuccess);
        Assert.Equal(KnownInstants.Epoch.AddHours(1), again.Value.Decision.ApprovedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveDecision_returns_UnknownDecision_for_an_unrecorded_id()
    {
        var result = await Approve();

        Assert.Equal(JudgeErrors.UnknownDecision.Code, result.Error.Code);
    }

    private Task<Kernel.Result<ApproveDecisionResponse>> Approve() =>
        new ApproveDecisionService(_decisions, _clock).Handle(A.ApproveDecisionRequest().Build(), CancellationToken.None).AsTask();
}
