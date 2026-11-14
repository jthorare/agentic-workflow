using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetStatus;

public sealed class GetStatusTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetStatusService Service() => new(_h.Held, new ProfileReads(_h.Profiles, _h.Profiles), _h.Store, new ProjectorProgress(_h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot());

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_reports_not_running_when_no_lease_is_held()
    {
        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.False(status.Running);
        Assert.Equal(0, status.Epoch);
        Assert.Null(status.Profile.Live);
        Assert.Empty(status.Services);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_reports_api_version_1() =>
        Assert.Equal(1, (await Service().Handle(new GetStatusRequest(), Token)).Value.ApiVersion);

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_reads_the_ledger_head_fresh_while_the_cursor_stays_the_projectors()
    {
        var grant = await _h.HoldLease();
        _ = await new RecordHeartbeatService(_h.Store, _h.Events, _h.Clock).Handle(new RecordHeartbeatRequest(new ScheduledRun(grant, _h.Clock.GetUtcNow())), Token);
        var head = (await _h.Store.Head(Token)).Value;

        var views = (await Service().Handle(new GetStatusRequest(), Token)).Value.Views;   // the projector hasn't seen any of it

        Assert.True(head > 0);
        Assert.Equal((0L, head), (views!.Cursor, views.LedgerHead));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_reports_epoch_services_and_the_last_heartbeat()
    {
        var approved = SliceHarness.Profile("5s");
        _h.Profiles.Live = approved;
        var grant = await _h.HoldLease();
        _ = await new RecordHeartbeatService(_h.Store, _h.Events, _h.Clock).Handle(new RecordHeartbeatRequest(new ScheduledRun(grant, _h.Clock.GetUtcNow())), Token);

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.True(status.Running);
        Assert.Equal(grant.Epoch, status.Epoch);
        Assert.Equal(approved.Hash, status.Profile.Live);
        var heartbeat = Assert.Single(status.Services);
        Assert.Equal(("heartbeat", "5s", _h.Clock.GetUtcNow()), (heartbeat.Name.Value, heartbeat.Every, heartbeat.LastRun!.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_answers_with_the_profile_problem_when_the_live_profile_is_invalid()
    {
        var grant = await _h.HoldLease();
        _ = await new RecordHeartbeatService(_h.Store, _h.Events, _h.Clock).Handle(new RecordHeartbeatRequest(new ScheduledRun(grant, _h.Clock.GetUtcNow())), Token);
        _h.Profiles.Live = Core.SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");

        var result = await Service().Handle(new GetStatusRequest(), Token);

        Assert.False(result.IsError);
        var status = result.Value;
        Assert.True(status.Running);
        Assert.Null(status.Profile.Live);
        Assert.Contains("/home/thor/profile.yaml", status.Profile.Problem, StringComparison.Ordinal);
    }
}
