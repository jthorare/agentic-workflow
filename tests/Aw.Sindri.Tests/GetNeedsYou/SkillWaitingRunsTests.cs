using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.SessionReports;
using Aw.Sindri.Tests.Views;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.GetNeedsYou;

/// <summary>The skill block runs Needs you shows as 'waiting on you' (skill flows spec §7, F-2a): an open skill launch whose last 'launch.owner-waiting' no 'launch.turn-started' has answered, in an instance nobody took over and still in the Step's state.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillWaitingRunsTests
{
    private static readonly InstanceAddress _step = InstanceAddress.From("ITM-1/step/1");
    private static readonly LaunchSkill _review = new("review", 1, Manifest: null, SkillMdSha: null, Push: false, Trial: false);

    private readonly SessionHarness _s = new();
    private readonly InstanceReads _instances = new();

    /// <summary>The instance a skill Step belongs to, as the engine reads it: in <see cref="State"/>, taken over or not, or unreadable.</summary>
    private sealed class InstanceReads : IInstanceReader
    {
        public string State { get; set; } = "review";

        public string? TakenOverBy { get; set; }

        public bool Fail { get; set; }

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                return ValueTask.FromResult<Result<InstanceView>>(WorkflowErrors.InstanceNotFound.With(address.Value));
            }

            var recipe = new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
            return ValueTask.FromResult<Result<InstanceView>>(new InstanceView(address, recipe, State, Detail: null, KnownInstants.Epoch, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
            {
                Id = WorkflowInstanceId.Of(address),
                TakenOverBy = TakenOverBy,
            });
        }
    }

    private async Task Waiting(string reason = "question", string? text = "Who calls this API?")
    {
        await _s.Started(instance: _step, skill: _review);
        await _s.Views.OwnerWaiting(reason, text, instance: _step, state: "review", attempt: 1);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
    }

    [Fact]
    public async Task A_skill_launch_with_an_owner_waiting_is_a_waiting_run_with_what_the_event_said()
    {
        await Waiting();

        var run = Assert.Single(await Runs().Find(Token));

        Expect.Equal((A.FirstLaunchId, A.FirstItemId, _step, "review", "review"), (run.Launch, run.Item, run.Instance, run.StateName, run.Skill));
        Expect.Equal(("question", "Who calls this API?"), (run.Waiting.Reason, run.Waiting.Text));
        Expect.Equal(_s.Now, run.Since);
    }

    [Fact]
    public async Task A_turn_started_after_the_owner_waiting_clears_the_run()
    {
        await Waiting();
        await _s.Views.TurnStarted(instance: _step, state: "review", attempt: 1);

        Assert.Empty(await Runs().Find(Token));
    }

    [Fact]
    public async Task A_second_owner_waiting_after_a_turn_started_is_a_waiting_run_again()
    {
        await Waiting(text: "first?");
        await _s.Views.TurnStarted();
        await _s.Views.OwnerWaiting(text: "second?", instance: _step, state: "review", attempt: 1);

        Expect.Equal("second?", Assert.Single(await Runs().Find(Token)).Waiting.Text);
    }

    [Fact]
    public async Task A_launch_that_is_not_a_skill_block_is_never_a_waiting_run()
    {
        await _s.Started(instance: _step);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);

        Assert.Empty(await Runs().Find(Token));
    }

    [Fact]
    public async Task A_session_that_ended_is_not_a_waiting_run()
    {
        await Waiting();
        await _s.Views.End();

        Assert.Empty(await Runs().Find(Token));
    }

    [Fact]
    public async Task A_Step_a_person_took_over_is_not_a_waiting_run()
    {
        await Waiting();
        _instances.TakenOverBy = "human:desk";

        Assert.Empty(await Runs().Find(Token));
    }

    [Fact]
    public async Task An_instance_that_moved_on_from_the_Step_is_not_a_waiting_run()
    {
        await Waiting();
        _instances.State = "build";

        Assert.Empty(await Runs().Find(Token));
    }

    [Fact]
    public async Task An_instance_that_cannot_be_read_is_left_off()
    {
        await Waiting();
        _instances.Fail = true;

        Assert.Empty(await Runs().Find(Token));
    }

    [Fact]
    public async Task A_launch_this_process_does_not_track_is_not_a_waiting_run()
    {
        await _s.Started(instance: _step, skill: _review);
        await _s.Views.OwnerWaiting(instance: _step, state: "review", attempt: 1);

        Assert.Empty(await Runs().Find(Token));
    }

    private sealed class NoProfileFacts : IRecipeProfileFacts
    {
        public ValueTask<Result<RecipeProfileFacts>> Current(CancellationToken cancellationToken) => throw new NotSupportedException("reading a recipe back needs no profile");
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SkillWaitingRuns Runs() => new(_s.Liveness, new LaunchViews(_s.Views.Views), _instances,
        new RecipeRegistryReader(new InMemoryRecipeStore(), new InMemoryRecipeApprovals(), EngineFixture.Catalog, SliceHarness.Json, new NoProfileFacts()));
}
