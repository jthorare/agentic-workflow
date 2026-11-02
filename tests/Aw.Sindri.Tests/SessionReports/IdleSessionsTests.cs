using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.SweepLostSessions;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.SessionReports;

/// <summary>The idle sweep: a Step session whose agent ended its turn with no report ends as idle after 120 s, unless a person took the Step over.</summary>
[Lane(TestLane.Unit)]
public sealed class IdleSessionsTests
{
    private readonly SessionHarness _s = new();
    private readonly FakeHostedSessions _hosted = new();
    private readonly InstanceReads _instances = new();
    private readonly FakeSessionNudges _nudges = new();
    private readonly InMemorySessionSnapshot _snapshot = new();
    private static readonly InstanceAddress _step = InstanceAddress.From("ITM-1/step/1");

    /// <summary>The instance a Step launch belongs to, as the engine reads it: taken over or not, or unreadable.</summary>
    private sealed class InstanceReads : IInstanceReader
    {
        public string? TakenOverBy { get; set; }

        public bool Fail { get; set; }

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                return ValueTask.FromResult<Result<InstanceView>>(WorkflowErrors.InstanceNotFound.With(address.Value));
            }

            var recipe = new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
            return ValueTask.FromResult<Result<InstanceView>>(new InstanceView(address, recipe, "produce", Detail: null, KnownInstants.Epoch, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
            {
                Id = WorkflowInstanceId.Of(address),
                TakenOverBy = TakenOverBy,
            });
        }
    }

    private async Task<int> SweepAfterTurnEnded(string? takenOverBy = null, bool unreadable = false)
    {
        await _s.Started(instance: _step);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.EndTurn(A.FirstLaunchId, _s.Now);
        (_instances.TakenOverBy, _instances.Fail) = (takenOverBy, unreadable);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(121));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        return await Sweeper().Sweep(Token);
    }

    [Fact]
    public async Task A_Step_not_taken_over_ends_as_idle_when_its_instance_is_read() => Expect.Equal(1, await SweepAfterTurnEnded());

    [Fact]
    public async Task A_Step_a_person_took_over_is_never_ended_as_idle()
    {
        Expect.Equal(0, await SweepAfterTurnEnded(takenOverBy: "human:desk"));
        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
        Expect.Equal(0, await Sweeper().Sweep(Token));   // still held on the next sweep

        _instances.TakenOverBy = null;   // handed back: the turn has been over all along
        Expect.Equal(1, await Sweeper().Sweep(Token));
    }

    [Fact]
    public async Task A_Step_whose_instance_cannot_be_read_is_not_ended_as_idle() => Expect.Equal(0, await SweepAfterTurnEnded(unreadable: true));

    [Fact]
    public async Task A_turn_that_ended_120s_ago_with_no_report_ends_the_session_as_idle_while_it_still_beats()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.EndTurn(A.FirstLaunchId, _s.Now);

        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(119));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        var early = await Sweeper().Sweep(Token);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(1));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        var swept = await Sweeper().Sweep(Token);

        Expect.Equal((0, 1), (early, swept));
        var recorded = Assert.Single(_s.Views.Slice.Store.Events);
        var payload = _s.Views.Slice.Events.Payload<SessionEnded>(recorded).Value;
        Expect.Equal(($"session.ended:{A.FirstLaunchId.Value}", SessionEndReason.Idle, null), (recorded.IdempotencyKey, payload.Reason, payload.ExitCode));
        Expect.Equal(A.FirstLaunchId, Assert.Single(_hosted.Forgotten));
    }

    [Fact]
    public async Task A_new_turn_starts_the_idle_clock_over()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.EndTurn(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(100));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.StartTurn(A.FirstLaunchId);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(100));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.EndTurn(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(100));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);

        Expect.Equal(0, await Sweeper().Sweep(Token));
        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
    }

    [Fact]
    public async Task A_session_whose_turn_never_ended_is_never_idle()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        for (var minute = 0; minute < 5; minute++)
        {
            _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(60));
            _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
            Expect.Equal(0, await Sweeper().Sweep(Token));
        }

        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
    }

    private async Task Started_and_stopped_on_an_API_error()
    {
        await _s.Started(instance: _step);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.FailTurn(A.FirstLaunchId, _s.Now);
    }

    private async Task<int> SweepAfter(TimeSpan wait)
    {
        _s.Views.Slice.Clock.Advance(wait);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        return await Sweeper().Sweep(Token);
    }

    [Fact]
    public async Task A_session_an_API_error_stopped_is_typed_one_re_prompt_after_120s_and_ended_only_if_it_stays_idle_10_min_more()
    {
        await Started_and_stopped_on_an_API_error();

        var early = await SweepAfter(TimeSpan.FromSeconds(119));
        var nudged = await SweepAfter(TimeSpan.FromSeconds(1));
        var later = await SweepAfter(TimeSpan.FromMinutes(9));   // 9 min after the nudge: still waiting
        var ended = await SweepAfter(TimeSpan.FromMinutes(1));

        Expect.Equal((0, 0, 0, 1), (early, nudged, later, ended));
        Expect.Equal(A.FirstLaunchId, Assert.Single(_nudges.Sent).Launch);
        var payload = _s.Views.Slice.Events.Payload<SessionEnded>(Assert.Single(_s.Views.Slice.Store.Events)).Value;
        Expect.Equal(SessionEndReason.Idle, payload.Reason);
    }

    [Fact]
    public async Task A_session_that_works_again_after_the_re_prompt_is_not_ended()
    {
        await Started_and_stopped_on_an_API_error();
        _ = await SweepAfter(TimeSpan.FromSeconds(120));

        _s.Files.StartTurn(A.FirstLaunchId);   // the re-prompt started a turn: the prompt hook deleted the marker
        var swept = await SweepAfter(TimeSpan.FromMinutes(30));

        Expect.Equal((0, 0, 1), (swept, _s.Views.Slice.Store.Events.Count, _nudges.Sent.Count));
    }

    [Fact]
    public async Task A_host_that_cannot_type_still_counts_as_nudged_so_the_session_ends_10_min_later()
    {
        _nudges.Accepts = false;
        await Started_and_stopped_on_an_API_error();

        _ = await SweepAfter(TimeSpan.FromSeconds(120));
        var ended = await SweepAfter(TimeSpan.FromMinutes(10));

        Expect.Equal((1, 1), (ended, _nudges.Sent.Count));
    }

    [Fact]
    public async Task A_session_with_a_tool_call_in_flight_is_never_nudged_or_ended()
    {
        await Started_and_stopped_on_an_API_error();
        _s.Files.SetToolRunning(A.FirstLaunchId, running: true);   // a 20-minute 'dotnet test'

        var swept = await SweepAfter(TimeSpan.FromMinutes(30));

        Expect.Equal((0, 0, 0), (swept, _nudges.Sent.Count, _s.Views.Slice.Store.Events.Count));
    }

    [Fact]
    public async Task A_session_the_collector_reads_as_idle_from_its_own_record_is_nudged_at_10_min_and_one_the_collector_only_guesses_is_not()
    {
        await _s.Started(instance: _step);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        var since = _s.Now;
        void Publish(bool estimated) => _snapshot.Publish(new SessionSnapshot(
            [new ObservedSession(new SessionRef(AgentId.From("claude-code"), SessionId.From("aaaaaaaa-0000-4000-8000-000000000001")), 1, 1, SessionState.Idle, StateSource.Record, since) { LaunchId = A.FirstLaunchId, Estimated = estimated }],
            [], [], _s.Now));

        _s.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(10));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        Publish(estimated: true);
        _ = await Sweeper().Sweep(Token);
        var guessed = _nudges.Sent.Count;
        Publish(estimated: false);
        _ = await Sweeper().Sweep(Token);

        Expect.Equal((0, 1), (guessed, _nudges.Sent.Count));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IdleSessions Sweeper() => new(_s.Liveness, _s.Events(), _s.Views.Slice.Clock, _hosted, _nudges, new IdleReads(_s.Files, _snapshot), new StepTakeovers(new LaunchViews(_s.Views.Views), _instances));
}
