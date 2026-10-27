using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.SweepLostSessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SessionReports;

/// <summary>The agent runs health and Needs you show (issue #1000): idle past <see cref="LaunchRules.IdleAfter"/> by the same rule the idle sweep ends them by, and not ended yet.</summary>
[Lane(TestLane.Unit)]
public sealed class StalledAgentRunsTests
{
    private static readonly InstanceAddress _step = InstanceAddress.From("ITM-1/step/1");
    private readonly SessionHarness _s = new();
    private readonly InstanceReads _instances = new();

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

    private async Task Idle(TimeSpan since)
    {
        await _s.Started(instance: _step);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.EndTurn(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(since);
    }

    [Fact]
    public async Task A_run_idle_for_the_idle_time_is_stalled_with_its_instance_and_when_its_turn_ended()
    {
        await Idle(TimeSpan.FromSeconds(120));

        var run = Assert.Single(await Runs().Find(Token));

        Expect.Equal((A.FirstLaunchId, _step), (run.Launch, run.Instance));
        Expect.Equal(_s.Now - TimeSpan.FromSeconds(120), run.TurnEndedAt);
    }

    [Fact]
    public async Task A_run_idle_for_less_than_the_idle_time_is_not_stalled()
    {
        await Idle(TimeSpan.FromSeconds(119));

        Assert.Empty(await Runs().Find(Token));
    }

    [Fact]
    public async Task A_run_whose_turn_never_ended_is_not_stalled()
    {
        await _s.Started(instance: _step);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Empty(await Runs().Find(Token));
    }

    [Fact]
    public async Task A_run_an_API_error_stopped_is_stalled_like_one_whose_turn_ended()
    {
        await _s.Started(instance: _step);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.FailTurn(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(120));

        Expect.Equal(A.FirstLaunchId, Assert.Single(await Runs().Find(Token)).Launch);
    }

    [Fact]
    public async Task A_run_with_a_tool_call_in_flight_is_not_stalled()
    {
        await Idle(TimeSpan.FromMinutes(5));
        _s.Files.SetToolRunning(A.FirstLaunchId, running: true);

        Assert.Empty(await Runs().Find(Token));
    }

    [Fact]
    public async Task A_Step_a_person_took_over_is_not_stalled()
    {
        await Idle(TimeSpan.FromMinutes(5));
        _instances.TakenOverBy = "human:desk";

        Assert.Empty(await Runs().Find(Token));
    }

    [Fact]
    public async Task A_run_whose_instance_cannot_be_read_is_listed_because_the_sweep_leaves_it_alone()
    {
        await Idle(TimeSpan.FromMinutes(5));
        _instances.Fail = true;

        _ = Assert.Single(await Runs().Find(Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private StalledAgentRuns Runs() => new(_s.Liveness, _s.Views.Slice.Clock, _s.Files, new LaunchViews(_s.Views.Views), _instances);
}
