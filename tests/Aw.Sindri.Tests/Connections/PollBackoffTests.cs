using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class PollBackoffTests
{
    [Fact]
    public void A_429_with_Retry_After_delays_the_next_poll_and_a_second_failure_doubles_it()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);

        var first = PollBackoff.Next(previousFailures: 0, retryAfter: TimeSpan.FromSeconds(30), clock.GetUtcNow());
        clock.Advance(TimeSpan.FromSeconds(30));
        var second = PollBackoff.Next(first.Failures, retryAfter: TimeSpan.FromSeconds(30), clock.GetUtcNow());

        Assert.Equal(1, first.Failures);
        Assert.Equal(KnownInstants.Epoch.AddSeconds(30), first.Until);
        Assert.Equal(2, second.Failures);
        Assert.Equal(KnownInstants.Epoch.AddSeconds(30 + 60), second.Until);
    }

    [Fact]
    public void Without_Retry_After_the_first_wait_is_the_base_delay_and_the_wait_is_capped()
    {
        var now = KnownInstants.Epoch;

        var first = PollBackoff.Next(previousFailures: 0, retryAfter: null, now);
        var long_run = PollBackoff.Next(previousFailures: 20, retryAfter: null, now);

        Assert.Equal(now + PollBackoff.BaseDelay, first.Until);
        Assert.Equal(now + PollBackoff.MaxDelay, long_run.Until);
        Assert.Equal(21, long_run.Failures);
    }

    [Fact]
    public void A_Retry_After_over_the_cap_is_honored_as_given()
    {
        var now = KnownInstants.Epoch;

        var first = PollBackoff.Next(previousFailures: 0, retryAfter: TimeSpan.FromHours(2), now);
        var second = PollBackoff.Next(first.Failures, retryAfter: TimeSpan.FromHours(2), now);

        Assert.Equal(now.AddHours(2), first.Until);
        Assert.Equal(now.AddHours(2), second.Until);
    }
}
