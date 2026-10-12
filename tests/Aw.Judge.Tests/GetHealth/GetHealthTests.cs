using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Health;
using Aw.Judge.Features.GetHealth;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.GetHealth;

public sealed class GetHealthTests
{
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch.AddDays(10));
    private readonly InMemoryDecisionLog _decisions = new();

    [Fact, Trait("Lane", "unit")]
    public async Task GetHealth_reports_ok_with_no_failures()
    {
        await Attempts(DecisionOutcome.Failed, DecisionOutcome.Failed, DecisionOutcome.Failed, DecisionOutcome.Failed, DecisionOutcome.Failed);

        var response = await Health();

        Assert.Equal(new GetHealthResponse(HealthStatus.Ok, 0), response);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetHealth_reports_down_when_the_last_five_attempts_failed_and_escalations_dont_break_the_streak()
    {
        await Failure(TimeSpan.FromHours(1));
        await Attempts(DecisionOutcome.Decided, DecisionOutcome.Failed, DecisionOutcome.Failed, DecisionOutcome.Failed, DecisionOutcome.Failed, DecisionOutcome.Escalated, DecisionOutcome.Failed);

        var response = await Health();

        Assert.Equal(new GetHealthResponse(HealthStatus.Down, 1), response);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetHealth_reports_degraded_when_a_recent_attempt_was_decided()
    {
        await Failure(TimeSpan.FromHours(2));
        await Failure(TimeSpan.FromHours(3));
        await Attempts(DecisionOutcome.Failed, DecisionOutcome.Failed, DecisionOutcome.Failed, DecisionOutcome.Failed, DecisionOutcome.Decided);

        var response = await Health();

        Assert.Equal(new GetHealthResponse(HealthStatus.Degraded, 2), response);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetHealth_counts_only_the_failures_of_the_last_24_hours()
    {
        await Failure(TimeSpan.FromHours(24));
        await Failure(TimeSpan.FromHours(24) + TimeSpan.FromMilliseconds(1));

        var response = await Health();

        Assert.Equal(new GetHealthResponse(HealthStatus.Degraded, 1), response);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetHealth_returns_the_store_error_when_the_store_cant_be_used()
    {
        var service = new GetHealthService(new InMemoryDecisionLog { Unavailable = true }, _clock);

        var result = await service.Handle(A.GetHealthRequest().Build(), CancellationToken.None);

        Assert.True(result.IsError);
    }

    private async Task<GetHealthResponse> Health()
    {
        var result = await new GetHealthService(_decisions, _clock).Handle(A.GetHealthRequest().Build(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private async Task Failure(TimeSpan ago) =>
        Assert.True((await _decisions.RecordFailure(new FailureRecord(_clock.GetUtcNow() - ago, "wake-gate", "claude-cli", "timeout"), CancellationToken.None)).IsSuccess);

    // Oldest first: the last one is the newest attempt.
    private async Task Attempts(params DecisionOutcome[] outcomes)
    {
        for (var i = 0; i < outcomes.Length; i++)
        {
            var decision = A.Decision().WithId("d" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).At(_clock.GetUtcNow() - TimeSpan.FromMinutes(outcomes.Length - i)).WithOutcome(outcomes[i]).Build();
            Assert.True((await _decisions.Record(decision, CancellationToken.None)).IsSuccess);
        }
    }
}
