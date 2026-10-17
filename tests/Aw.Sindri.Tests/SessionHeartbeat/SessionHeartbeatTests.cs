using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.SessionHeartbeat;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.SessionReports;

namespace Aw.Sindri.Tests.SessionHeartbeat;

/// <summary>'session wrap''s heartbeat (API spec §8.1, launcher L-9): proven by the launch secret, it keeps the session alive and carries the agent's own session id once.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionHeartbeatTests
{
    private const string Uuid = "0a1b2c3d-0000-4000-8000-000000000001";
    private readonly SessionHarness _s = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SessionHeartbeatService Service(params Core.Launch.AgentEntry[] catalog) => new(_s.Events(catalog));

    private IReadOnlyList<Core.Ledger.LedgerEvent> Recorded() => _s.Views.Slice.Store.Events;

    [Fact]
    public async Task SessionHeartbeat_refuses_a_wrong_launch_secret()
    {
        await _s.Started();

        var result = await Service().Handle(A.SessionHeartbeatRequest().WithProof("not-the-proof").Build(), Token);

        Expect.True(result.IsError && string.Equals(result.Error.Code, SindriErrors.AuthLaunchSecret.Code, StringComparison.Ordinal));
        Expect.Equal(0, Recorded().Count);
        Expect.Equal(0, _s.Liveness.Silent(_s.Now.AddDays(1), LaunchRules.LostAfter).Count);
    }

    [Fact]
    public async Task A_heartbeat_for_a_launch_that_was_never_opened_is_refused()
    {
        await _s.Started();
        var other = LaunchId.From("LCH-00000000000000000000000000000002");

        var result = await Service().Handle(A.SessionHeartbeatRequest().WithLaunchId(other).Build(), Token);

        Expect.True(result.IsError && string.Equals(result.Error.Code, SindriErrors.AuthLaunchSecret.Code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_heartbeat_starts_the_120s_silence_clock()
    {
        await _s.Started();

        var result = await Service().Handle(A.SessionHeartbeatRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal(A.FirstLaunchId, result.Value.Launch);
        Expect.Equal(0, _s.Liveness.Silent(_s.Now.AddSeconds(119), LaunchRules.LostAfter).Count);
        Expect.Equal(1, _s.Liveness.Silent(_s.Now.AddSeconds(120), LaunchRules.LostAfter).Count);
    }

    [Fact]
    public async Task First_heartbeat_with_session_id_appends_session_reported()
    {
        await _s.Started(target: "codex");

        var result = await Service().Handle(A.SessionHeartbeatRequest().WithSessionId(Uuid).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(Recorded());
        var payload = _s.Views.Slice.Events.Payload<LaunchSessionReported>(recorded).Value;
        Expect.Equal((EventTypes.LaunchSessionReported, $"launch.session-reported:{A.FirstLaunchId.Value}", A.FirstItemId.Value), (recorded.Envelope.Type, recorded.IdempotencyKey, recorded.Item));
        Expect.Equal((A.FirstLaunchId, Uuid), (payload.Launch, payload.SessionId.Value));
    }

    [Fact]
    public async Task A_heartbeat_that_repeats_the_id_appends_nothing_more()
    {
        await _s.Started(target: "codex");
        var service = Service();

        _ = await service.Handle(A.SessionHeartbeatRequest().WithSessionId(Uuid).Build(), Token);
        var again = await service.Handle(A.SessionHeartbeatRequest().WithSessionId(Uuid).Build(), Token);

        Assert.True(again.IsSuccess);
        _ = Assert.Single(Recorded());
    }

    [Fact]
    public async Task An_id_the_entrys_pattern_refuses_is_not_recorded_but_the_beat_counts()
    {
        await _s.Started(target: "codex");

        var result = await Service().Handle(A.SessionHeartbeatRequest().WithSessionId("not-a-uuid").Build(), Token);

        Assert.True(result.IsSuccess);
        Expect.Equal(0, Recorded().Count);
        Expect.Equal(1, _s.Liveness.Silent(_s.Now.AddMinutes(5), LaunchRules.LostAfter).Count);
    }

    [Fact]
    public async Task Two_concurrent_reported_launches_record_their_own_ids()
    {
        var second = LaunchId.From("LCH-00000000000000000000000000000002");
        await _s.Started(target: "codex");
        await _s.Started(second, target: "codex");
        const string OtherUuid = "0a1b2c3d-0000-4000-8000-000000000002";
        var service = Service();

        _ = await service.Handle(A.SessionHeartbeatRequest().WithSessionId(Uuid).Build(), Token);
        _ = await service.Handle(A.SessionHeartbeatRequest().WithLaunchId(second).WithSessionId(OtherUuid).Build(), Token);

        var reported = Recorded().Select(e => _s.Views.Slice.Events.Payload<LaunchSessionReported>(e).Value).ToDictionary(static r => r.Launch.Value, static r => r.SessionId.Value, StringComparer.Ordinal);
        Expect.Equal((Uuid, OtherUuid), (reported[A.FirstLaunchId.Value], reported[second.Value]));
    }
}
