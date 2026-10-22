using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Tests.PrWatch;

[Lane(TestLane.Unit)]
public sealed class PrWatchServiceTests
{
    private const string Link = PrWatchRig.Link;
    private const string Other = "https://forge.test/acme/widgets/pull/8";
    private readonly PrWatchRig _rig = new();

    [Fact]
    public async Task Nothing_waiting_is_quiet_and_asks_the_forge_nothing()
    {
        var result = await _rig.Poll();

        Assert.True(result.Value.Idle);
        Assert.Empty(_rig.Digests.Calls);
    }

    [Fact]
    public async Task A_pull_request_seen_first_is_recorded_and_writes_no_event()
    {
        _rig.Watched.Watch(Link, "ITM-1");
        _rig.Digests.Set(Link, "t1|aaa|pending|0");

        var result = await _rig.Poll();

        Assert.True(result.Value.Idle);
        Assert.Empty(await _rig.Changed());
        Assert.Equal("t1|aaa|pending|0", _rig.State.Last.Pulls[Link].Digest);
    }

    [Fact]
    public async Task A_moved_head_appends_one_updated_event_for_the_waiting_item_with_the_instances_link_as_subject()
    {
        _rig.Watched.Watch(Link, "ITM-1");
        _rig.Digests.Set(Link, "t1|aaa|pending|0");
        _ = await _rig.Poll();
        _rig.Clock.Advance(TimeSpan.FromMinutes(1));
        _rig.Digests.Set(Link, "t2|bbb|pending|0", head: "bbb");

        var result = await _rig.Poll();

        var row = Assert.Single(await _rig.Changed());
        using var fact = _rig.PrEvent(row);
        Assert.False(result.Value.Idle);
        Assert.Equal(("service:pr-watch", "ITM-1", "updated"), (row.Envelope.Actor, row.Item, PrWatchRig.Outcome(row)));
        Assert.Equal((Link, "bbb", "updated"), (fact.RootElement.GetProperty("subject").GetString(), fact.RootElement.GetProperty("headSha").GetString(), fact.RootElement.GetProperty("changes")[0].GetString()));
    }

    [Fact]
    public async Task An_unchanged_pull_request_writes_nothing_on_later_polls()
    {
        _rig.Watched.Watch(Link, "ITM-1");
        _rig.Digests.Set(Link, "t1|aaa|pending|0");

        _ = await _rig.Poll();
        _rig.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await _rig.Poll();

        Assert.True(second.Value.Idle);
        Assert.Empty(await _rig.Changed());
    }

    [Fact]
    public async Task A_merge_outranks_the_other_changes_and_all_are_listed()
    {
        _rig.Watched.Watch(Link, "ITM-1");
        _rig.Digests.Set(Link, "t1|aaa|pending|0");
        _ = await _rig.Poll();
        _rig.Digests.Set(Link, "t2|bbb|success|0", PullState.Merged, "bbb");

        _ = await _rig.Poll();

        var row = Assert.Single(await _rig.Changed());
        using var fact = _rig.PrEvent(row);
        Assert.Equal("merged", PrWatchRig.Outcome(row));
        Assert.Equal(new[] { "merged", "updated", "ci" }, fact.RootElement.GetProperty("changes").EnumerateArray().Select(static c => c.GetString()));
    }

    [Fact]
    public async Task A_quiet_period_appends_one_idle_then_waits_a_period_again()
    {
        _rig.Remind.After = TimeSpan.FromHours(1);
        _rig.Watched.Watch(Link, "ITM-1");
        _rig.Digests.Set(Link, "t1|aaa|pending|0");
        _ = await _rig.Poll();

        _rig.Clock.Advance(TimeSpan.FromMinutes(59));
        _ = await _rig.Poll();
        _rig.Clock.Advance(TimeSpan.FromMinutes(2));
        _ = await _rig.Poll();
        _rig.Clock.Advance(TimeSpan.FromMinutes(1));
        _ = await _rig.Poll();
        _rig.Clock.Advance(TimeSpan.FromMinutes(60));
        _ = await _rig.Poll();

        Assert.Equal(new[] { "idle", "idle" }, (await _rig.Changed()).Select(PrWatchRig.Outcome));
    }

    [Fact]
    public async Task Pull_requests_on_one_host_are_one_digest_call_and_another_host_is_its_own()
    {
        const string Elsewhere = "https://other.test/acme/widgets/pull/1";
        _rig.Watched.Watch(Link, "ITM-1");
        _rig.Watched.Watch(Other, "ITM-2");
        _rig.Watched.Watch(Elsewhere, "ITM-3");

        _ = await _rig.Poll();

        Assert.Equal(new[] { "https://forge.test/acme/widgets/pull/7 https://forge.test/acme/widgets/pull/8", Elsewhere }, _rig.Digests.Calls.Select(static c => string.Join(' ', c)).OrderBy(static c => c, StringComparer.Ordinal));
    }

    [Fact]
    public async Task An_unreachable_host_backs_off_doubling_and_is_unreachable_after_three_polls()
    {
        _rig.Watched.Watch(Link, "ITM-1");
        _rig.Digests.Failure = SindriErrors.ForgeUnavailable.With("down");

        var first = await _rig.Poll();
        _rig.Clock.Advance(TimeSpan.FromSeconds(30));
        _ = await _rig.Poll();
        var callsInBackoff = _rig.Digests.Calls.Count;
        _rig.Clock.Advance(TimeSpan.FromSeconds(31));
        var second = await _rig.Poll();
        _rig.Clock.Advance(TimeSpan.FromMinutes(1));
        _ = await _rig.Poll();   // still backing off: two minutes after the second failure
        _rig.Clock.Advance(TimeSpan.FromMinutes(1));
        var third = await _rig.Poll();

        Assert.True(first.Value.Idle);
        Assert.Equal(1, callsInBackoff);   // no second call within the first minute
        Assert.True(second.Value.Idle);
        Assert.Equal("SND-FORGE-006", third.Error.Code);
    }

    [Fact]
    public async Task A_host_that_recovers_forgets_its_back_off()
    {
        _rig.Watched.Watch(Link, "ITM-1");
        _rig.Digests.Failure = SindriErrors.ForgeUnavailable.With("down");
        _ = await _rig.Poll();
        _rig.Clock.Advance(TimeSpan.FromMinutes(2));
        _rig.Digests.Failure = null;
        _rig.Digests.Set(Link, "t1|aaa|pending|0");

        _ = await _rig.Poll();

        Assert.Empty(_rig.State.Last.Hosts);
    }

    [Fact]
    public async Task A_refused_call_is_read_again_once_then_the_service_is_degraded_with_the_credential_code()
    {
        _rig.Watched.Watch(Link, "ITM-1");
        _rig.Digests.Failure = SindriErrors.ForgeRefused.With("401");

        var result = await _rig.Poll();

        Assert.Equal("SND-FORGE-005", result.Error.Code);
        Assert.Equal(2, _rig.Digests.Calls.Count);
    }

    [Fact]
    public async Task A_pull_request_nobody_waits_on_any_more_is_forgotten()
    {
        _rig.Watched.Watch(Link, "ITM-1");
        _rig.Digests.Set(Link, "t1|aaa|pending|0");
        _ = await _rig.Poll();

        _rig.Watched.Unwatch(Link);
        _ = await _rig.Poll();

        Assert.Empty(_rig.State.Last.Pulls);
    }

    [Fact]
    public async Task A_listing_that_fails_fails_the_run_and_touches_nothing()
    {
        _rig.Watched.Failure = SindriErrors.ApiTimeout.With("temporal is down");

        var result = await _rig.Poll();

        Assert.True(result.IsError);
        Assert.Empty(_rig.Digests.Calls);
    }
}
