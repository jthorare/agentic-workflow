using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Launching;

/// <summary>A restart while a skill run waits on the owner (skill flows spec §7, F-2a): the start-up sweep adopts the live launch, so the waiting run is found again from the launches view, with no new event.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillWaitingAdoptionTests
{
    private static readonly LaunchId _a = LaunchId.From("LCH-" + new string('0', 31) + "1");
    private static readonly InstanceAddress _step = InstanceAddress.From("ITM-1/step/1");

    private readonly LauncherRig _rig = new();

    private sealed class OpenInstance : IInstanceReader
    {
        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken)
        {
            var recipe = new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
            return ValueTask.FromResult<Result<InstanceView>>(new InstanceView(address, recipe, "review", Detail: null, KnownInstants.Epoch, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
            {
                Id = WorkflowInstanceId.Of(address),
            });
        }
    }

    private sealed class NoProfileFacts : IRecipeProfileFacts
    {
        public ValueTask<Result<RecipeProfileFacts>> Current(CancellationToken cancellationToken) => throw new NotSupportedException("reading a recipe back needs no profile");
    }

    private SkillWaitingRuns Runs() => new(_rig.Liveness, new LaunchViews(_rig.H.Views), new OpenInstance(),
        new RecipeRegistryReader(new InMemoryRecipeStore(), new InMemoryRecipeApprovals(), EngineFixture.Catalog, SliceHarness.Json, new NoProfileFacts()));

    [Fact]
    public async Task A_restart_adopts_a_live_skill_launch_waiting_on_the_owner_and_it_is_a_waiting_run_again()
    {
        await _rig.Boot();
        await _rig.Item();
        _ = await _rig.Tick();
        using var caller = _rig.Caller();
        await _rig.Request(_a);
        var started = new LaunchStarted(_a, A.FirstItemId, LaunchMode.Open, AgentId.From("claude-code"), SessionId: null, "sindri", new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Opened, Code: null, Detail: null),
            ["tmux", "attach"], CatalogSource.Builtin, "sha256:abc", _step, "review", 1, Skill: new LaunchSkill("review", 1, Manifest: null, SkillMdSha: null, Push: false, Trial: false));
        await _rig.Append(EventTypes.LaunchStarted, started, "launch.outcome:" + _a.Value, A.FirstItemId);
        await _rig.Append(EventTypes.LaunchOwnerWaiting, new LaunchOwnerWaiting(_a, A.FirstItemId, "question", "Who calls this API?", "/run/.sindri-out/result-1.json", Detail: null, _step, "review", 1), "launch.owner-waiting:" + _a.Value + ":1", A.FirstItemId);

        _rig.Restart();   // nothing remembers a launch now

        Assert.Empty(await Runs().Find(TestContext.Current.CancellationToken));   // before the sweep has run, as the plan says
        _ = await _rig.Tick();

        var run = Assert.Single(await Runs().Find(TestContext.Current.CancellationToken));
        Assert.Equal((_a, "question"), (run.Launch, run.Waiting.Reason));
    }
}
