using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.SampleSessionUsage;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.SessionReports;

/// <summary>The usage sample (SCR E-28): a live session that knows its agent session id gets a private 'session.usage' when its tokens changed.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionUsageSamplerTests
{
    private static readonly SessionId _agentSession = SessionId.From("11111111-2222-3333-4444-555555555555");
    private readonly SessionHarness _s = new();
    private readonly UsageSamples _samples = new();
    private readonly Dictionary<SessionId, IReadOnlyList<ModelTokens>> _used = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SessionUsageSampler Sampler() => new(_s.Liveness, new LaunchViews(_s.Views.Views), new Used(_used), _samples, _s.Events(), _s.Views.Slice.Clock);

    private async Task Running()
    {
        await _s.Started();
        await _s.Views.Reported(_agentSession);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
    }

    [Fact]
    public async Task A_live_session_gets_a_private_sample_keyed_by_launch_and_number()
    {
        await Running();
        _used[_agentSession] = [new ModelTokens("m", Input: 10, CacheRead: 5, CacheWrite: 1, Output: 7)];
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(60));

        var appended = await Sampler().Sample(Token);

        Expect.Equal(1, appended);
        var recorded = Assert.Single(_s.Views.Slice.Store.Events);
        var payload = _s.Views.Slice.Events.Payload<SessionUsage>(recorded).Value;
        Expect.Equal(($"session.usage:{A.FirstLaunchId.Value}:2", 2, 16L, 7L), (recorded.IdempotencyKey, payload.Sample, payload.InputTokens, payload.OutputTokens));
    }

    [Fact]
    public async Task Unchanged_totals_write_nothing_and_changed_ones_write_the_next_sample()
    {
        await Running();
        _used[_agentSession] = [new ModelTokens("m", 10, 0, 0, 7)];
        _ = await Sampler().Sample(Token);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(60));
        var same = await Sampler().Sample(Token);
        _used[_agentSession] = [new ModelTokens("m", 20, 0, 0, 9)];
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(60));
        var changed = await Sampler().Sample(Token);

        Expect.Equal((0, 1, 2), (same, changed, _s.Views.Slice.Store.Events.Count));
    }

    [Fact]
    public async Task A_session_without_an_agent_id_or_reader_is_not_sampled()
    {
        await _s.Started();   // no 'launch.session-reported', and the started event carries no id
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _used[_agentSession] = [new ModelTokens("m", 10, 0, 0, 7)];

        var withoutId = await Sampler().Sample(Token);
        await _s.Views.Reported(_agentSession);
        _used.Clear();
        var withoutReader = await Sampler().Sample(Token);

        Expect.Equal((0, 0, 0), (withoutId, withoutReader, _s.Views.Slice.Store.Events.Count));
    }

    [Fact]
    public async Task An_ended_session_is_not_sampled()
    {
        await Running();
        await _s.Views.End();
        _used[_agentSession] = [new ModelTokens("m", 10, 0, 0, 7)];

        Expect.Equal(0, await Sampler().Sample(Token));
    }

    private sealed class Used(Dictionary<SessionId, IReadOnlyList<ModelTokens>> used) : ILaunchUsage
    {
        public ValueTask<IReadOnlyList<ModelTokens>?> OfSession(AgentId target, SessionId session, CancellationToken cancellationToken) =>
            ValueTask.FromResult(used.TryGetValue(session, out var rows) ? rows : null);
    }
}
