using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Features.SampleSessionUsage;
using Aw.Sindri.Features.SessionHeartbeat;
using Aw.Sindri.Features.SweepLostSessions;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.SessionReports;

/// <summary>What the review of the launcher PR (#295, run 1) found in session liveness, usage and the end of a session.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionReviewFixTests
{
    private static readonly SessionId _agentSession = SessionId.From("11111111-2222-3333-4444-555555555555");
    private readonly SessionHarness _s = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LostSessions Sweeper() => new(_s.Liveness, _s.Events(), _s.Views.Slice.Clock, _s.Files, new LaunchViews(_s.Views.Views), _s.Views.Progress);

    private SessionUsageSampler Sampler(UsageSamples samples) =>
        new(_s.Liveness, new LaunchViews(_s.Views.Views), new TokensReader(_s), samples, _s.Events(), _s.Views.Slice.Clock);

    [Fact]
    public async Task A_heartbeat_that_comes_back_an_error_does_not_restart_the_silence_clock()
    {
        await _s.Started(target: "codex");
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(100));
        var withoutLease = new SessionHeartbeatService(_s.Events(new HeldLease()));

        var refused = await withoutLease.Handle(A.SessionHeartbeatRequest().WithSessionId("0a1b2c3d-0000-4000-8000-000000000001").Build(), Token);

        Expect.Equal(SindriErrors.NotRunning.Code, refused.Error.Code);
        Expect.Equal(1, _s.Liveness.Silent(_s.Now.AddSeconds(20), LaunchRules.LostAfter).Count);   // 120 s after the first beat, not after the refused one
    }

    [Fact]
    public async Task A_sweep_that_comes_late_after_a_sleep_restarts_every_clock_instead_of_ending_the_sessions()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        var sweeper = Sweeper();
        Expect.Equal(0, await sweeper.Sweep(Token));   // the sweep runs every 10 s
        _s.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(10));   // the machine slept: the wrap had no time to beat

        var afterSleep = await sweeper.Sweep(Token);
        var stillSilent = 0;
        for (var tick = 0; tick < 13; tick++)   // two more minutes of real silence, swept every 10 s as the host does, ends it
        {
            _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(10));
            stillSilent += await sweeper.Sweep(Token);
        }

        Expect.Equal((0, 1), (afterSleep, stillSilent));
    }

    [Fact]
    public async Task A_session_that_ends_records_its_final_tokens()
    {
        await _s.Started();
        await _s.Views.Reported(_agentSession);
        _s.Tokens[_agentSession] = [new ModelTokens("m-small", 10, 5, 1, 7), new ModelTokens("m-big", 100, 0, 0, 40)];

        var ended = await _s.Events().End(A.FirstLaunchId, SessionEndReason.Exit, 0, Token);

        Assert.True(ended.IsSuccess, ended.IsError ? ended.Error.ToString() : null);
        var payload = _s.Views.Slice.Events.Payload<SessionEnded>(Assert.Single(_s.Views.Slice.Store.Events)).Value;
        Expect.Equal((116L, 47L), (payload.InputTokens, payload.OutputTokens));
    }

    [Fact]
    public async Task A_session_with_no_transcript_ends_without_tokens()
    {
        await _s.Started();

        _ = await _s.Events().End(A.FirstLaunchId, SessionEndReason.Lost, exitCode: null, Token);

        var payload = _s.Views.Slice.Events.Payload<SessionEnded>(Assert.Single(_s.Views.Slice.Store.Events)).Value;
        Expect.Equal((null, null), (payload.InputTokens, payload.OutputTokens));
    }

    [Fact]
    public async Task A_usage_sample_names_the_target_and_the_model_with_the_most_tokens()
    {
        await _s.Started(target: "codex");
        await _s.Views.Reported(_agentSession);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Tokens[_agentSession] = [new ModelTokens("m-small", 10, 0, 0, 1), new ModelTokens("m-big", 100, 0, 0, 40)];

        _ = await Sampler(new UsageSamples()).Sample(Token);

        var sample = _s.Views.Slice.Events.Payload<SessionUsage>(Assert.Single(_s.Views.Slice.Store.Events)).Value;
        Expect.Equal(("codex", "m-big"), (sample.Target.Value, sample.Model));
    }

    [Fact]
    public async Task Two_changes_within_one_minute_are_two_samples_not_a_duplicate_key()
    {
        await _s.Started();
        await _s.Views.Reported(_agentSession);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        var samples = new UsageSamples();
        _s.Tokens[_agentSession] = [new ModelTokens("m", 10, 0, 0, 1)];
        _ = await Sampler(samples).Sample(Token);
        _s.Tokens[_agentSession] = [new ModelTokens("m", 20, 0, 0, 2)];

        var second = await Sampler(samples).Sample(Token);

        Expect.Equal((1, 2), (second, _s.Views.Slice.Store.Events.Count));
    }

    [Fact]
    public async Task A_sample_that_could_not_be_appended_is_tried_again_not_marked_seen()
    {
        await _s.Started();
        await _s.Views.Reported(_agentSession);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Tokens[_agentSession] = [new ModelTokens("m", 10, 0, 0, 1)];
        var samples = new UsageSamples();
        var withoutLease = new SessionUsageSampler(_s.Liveness, new LaunchViews(_s.Views.Views), new TokensReader(_s), samples, _s.Events(new HeldLease()), _s.Views.Slice.Clock);

        var dropped = await withoutLease.Sample(Token);
        var retried = await Sampler(samples).Sample(Token);

        Expect.Equal((0, 1), (dropped, retried));
    }

    private sealed class TokensReader(SessionHarness harness) : ILaunchUsage
    {
        public ValueTask<IReadOnlyList<ModelTokens>?> OfSession(Core.Launch.AgentId target, SessionId session, CancellationToken cancellationToken) =>
            ValueTask.FromResult(harness.Tokens.TryGetValue(session, out var rows) ? rows : null);
    }
}
