using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.SessionExit;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.SessionReports;

namespace Aw.Sindri.Tests.SessionExit;

/// <summary>'session wrap''s exit call (API spec §8.1): proven by the launch secret, it ends the session with the agent's exit code and deletes the launch's files.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionExitTests
{
    private const string Uuid = "0a1b2c3d-0000-4000-8000-000000000001";
    private readonly SessionHarness _s = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SessionExitService Service() => new(_s.Events());

    [Fact]
    public async Task SessionExit_records_session_ended_with_the_exit_code()
    {
        await _s.Started();

        var result = await Service().Handle(A.SessionExitRequest().WithExitCode(7).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_s.Views.Slice.Store.Events);
        var payload = _s.Views.Slice.Events.Payload<SessionEnded>(recorded).Value;
        Expect.Equal((EventTypes.SessionEnded, $"session.ended:{A.FirstLaunchId.Value}", A.FirstItemId.Value), (recorded.Envelope.Type, recorded.IdempotencyKey, recorded.Item));
        Expect.Equal((A.FirstLaunchId, SessionEndReason.Exit, 7), (payload.Launch, payload.Reason, payload.ExitCode));
    }

    [Fact]
    public async Task A_wrong_secret_is_SND_AUTH_003_and_records_nothing()
    {
        await _s.Started();

        var result = await Service().Handle(A.SessionExitRequest().WithProof("not-the-proof").Build(), Token);

        Expect.True(result.IsError && string.Equals(result.Error.Code, SindriErrors.AuthLaunchSecret.Code, StringComparison.Ordinal));
        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
        Expect.Equal(0, _s.Files.Forgotten.Count);
    }

    [Fact]
    public async Task The_reported_id_is_recorded_before_the_end()
    {
        await _s.Started(target: "codex");

        _ = await Service().Handle(A.SessionExitRequest().WithSessionId(Uuid).Build(), Token);

        Assert.Equal([EventTypes.LaunchSessionReported, EventTypes.SessionEnded], _s.Views.Slice.Store.Events.OrderBy(static e => e.Sequence).Select(static e => e.Envelope.Type), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Ending_deletes_the_launchs_files_and_a_late_call_cannot_validate()
    {
        await _s.Started();
        var service = Service();

        _ = await service.Handle(A.SessionExitRequest().Build(), Token);
        var late = await service.Handle(A.SessionExitRequest().Build(), Token);

        Expect.Equal(A.FirstLaunchId, Assert.Single(_s.Files.Forgotten));
        Expect.True(late.IsError && string.Equals(late.Error.Code, SindriErrors.AuthLaunchSecret.Code, StringComparison.Ordinal));
        _ = Assert.Single(_s.Views.Slice.Store.Events);
    }

    [Fact]
    public async Task Ending_forgets_the_silence_clock()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);

        _ = await Service().Handle(A.SessionExitRequest().Build(), Token);

        Expect.Equal(0, _s.Liveness.Silent(_s.Now.AddDays(1), LaunchRules.LostAfter).Count);
    }
}
