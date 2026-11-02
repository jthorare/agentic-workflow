using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.Setup;

/// <summary>A map build waiting to retry (setup-once spec §6, §8).</summary>
public sealed class SetupStepsDeferredAndRetryTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_map_build_waiting_to_retry_is_running_with_the_wait_and_the_reason_and_offers_the_retry()
    {
        var fact = new MapFact(MapPhase.Building, new SetupProgress(0, 1, "files")) { Retry = new MapRetry(TimeSpan.FromMinutes(5), "the code index was busy") };

        var step = SetupSteps.Map(fact);

        Assert.Equal((SetupState.Running, "Retrying in 5 minutes: the code index was busy.", "RefreshContextMap"), (step.State, step.Detail, step.Action));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_retry_of_one_minute_is_said_in_the_singular()
    {
        var step = SetupSteps.Map(new MapFact(MapPhase.Building) { Retry = new MapRetry(TimeSpan.FromMinutes(1), "the code index was busy") });

        Assert.Equal("Retrying in 1 minute: the code index was busy.", step.Detail);
    }
}
