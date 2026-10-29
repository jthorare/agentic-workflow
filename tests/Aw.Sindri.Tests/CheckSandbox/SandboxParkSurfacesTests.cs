using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Items;
using Aw.Sindri.Tests.Workflow.Engine;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.CheckSandbox;

/// <summary>A park for a sandbox refusal carries the refusal in the owner's words (sandbox spec §8) on the needs-you row and the instance status, built through the engine's own decision code.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxParkSurfacesTests
{
    private const string ProduceStep = "step.produce";
    private static readonly RunToken _run = RunToken.For(1, 1);
    private static readonly DateTimeOffset _since = new(2026, 10, 28, 8, 0, 0, TimeSpan.Zero);

    private static WorkflowInstance Running()
    {
        var at = At(Step, "produce");
        var loops = new Dictionary<string, LoopState>(StringComparer.Ordinal) { ["revisions"] = new LoopState(3, 3, Last: null) };
        return at with { Frame = at.Frame with { Run = new OpenRun(_run, StepRuntime.Agent, "LCH-" + new string('a', 32), Task: null) }, Loops = loops };
    }

    private static DecisionContext AgentOnly()
    {
        var blocks = EngineFixture.Catalog.Blocks.Select(static b => b.Id.Value == ProduceStep ? b with { DeskCapable = false } : b);
        return EngineFixture.Context(ProduceStep) with { Catalog = new BlockCatalog([.. blocks], EngineFixture.Catalog.Events) };
    }

    private static WorkflowInstance Park(Trigger trigger) =>
        Assert.IsType<Applied>(Decide(Running(), Step, trigger, context: AgentOnly())).Changes[0].After;

    private static WorkflowInstance NoBwrap() =>
        Park(new RuntimeUnavailable("service:agent:" + ProduceStep, _run, "SND-LAUNCH-014", "no-bwrap", Source: null, "no sandbox tool"));

    private static WorkflowInstance LoginPark() =>
        Park(new LaunchRefused("service:agent:" + ProduceStep, _run, "SND-LAUNCH-017", "expired", Source: "owner-copy", "the login ran out"));

    private static InstanceView View(WorkflowInstance parked) =>
        InstanceViews.Of(parked, StatusCases.Recipe with { Recipe = Step, Catalog = EngineFixture.Catalog }, DateTimeOffset.UnixEpoch, timeoutAt: null, new InstanceViewContext());

    private static ItemRow Item(InstanceAddress address) => new(
        ItemQueryHarness.Id('1'), "the item", RepoName.From("agentic-workflow"), ItemState.Parked, Step: "produce", Version: 2, "human:thor", _since, _since, _since,
        Link: null, Alias: null, Body: null, WaitingOn: null, Instance: address.Value, Owner: null, Sequence: 3);

    [Fact]
    public void The_status_of_a_sandbox_park_says_the_check_again_sentence()
    {
        var status = InstanceViews.Status(View(NoBwrap()), DateTimeOffset.UnixEpoch);

        var sandbox = Assert.IsType<StatusSandbox>(status.Sandbox);
        Assert.Equal(("SND-LAUNCH-014", "no-bwrap", null), (sandbox.Code, sandbox.Reason, sandbox.Source));
        Assert.Equal("This machine has no sandbox tool, so this Step can't run by itself. Install bubblewrap (`sudo apt install bubblewrap`), then press Check again.", sandbox.Text);
        Assert.Equal("sudo apt install bubblewrap", sandbox.Command);
        Assert.Null(sandbox.State);
    }

    [Fact]
    public void A_login_park_names_its_credential_source()
    {
        var status = InstanceViews.Status(View(LoginPark()), DateTimeOffset.UnixEpoch);

        var sandbox = Assert.IsType<StatusSandbox>(status.Sandbox);
        Assert.Equal(("SND-LAUNCH-017", "expired", "owner-copy"), (sandbox.Code, sandbox.Reason, sandbox.Source));
        Assert.Contains("Check again", sandbox.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_park_for_any_other_reason_has_no_sandbox_part()
    {
        var parked = Park(new RuntimeUnavailable("service:agent:" + ProduceStep, _run, "SND-WF-010", Reason: null, Source: null, "no runtime"));
        var status = InstanceViews.Status(View(parked), DateTimeOffset.UnixEpoch);

        Assert.Null(status.Sandbox);
    }

    [Fact]
    public void The_events_summary_of_a_suspension_carries_the_sandbox_refusal()
    {
        var summary = WorkflowEventDelta.Summary(NoBwrap().Suspension);

        Assert.Equal(new SandboxPark("SND-LAUNCH-014", "no-bwrap", Source: null), summary!.Sandbox);
    }

    [Fact]
    public void The_park_row_carries_the_same_sentence_as_the_status()
    {
        var parked = NoBwrap();
        var summary = WorkflowEventDelta.Summary(parked.Suspension)!;

        var row = NeedsYouRows.ForPark(Item(parked.Address), new ParkRowInput(parked.Address, summary), _since, new PassThroughScrubber());

        var status = InstanceViews.Status(View(parked), DateTimeOffset.UnixEpoch);
        Assert.Equal(status.Sandbox, row.Sandbox);
    }

    [Fact]
    public void A_row_with_no_sandbox_refusal_leaves_the_field_out_of_its_json()
    {
        var summary = new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null);
        var address = InstanceAddress.From("ITM-11111111111111111111111111111111/step/1");

        var row = NeedsYouRows.ForPark(Item(address), new ParkRowInput(address, summary), _since, new PassThroughScrubber());

        Assert.Null(row.Sandbox);
        Assert.DoesNotContain("sandbox", JsonSerializer.Serialize(row), StringComparison.OrdinalIgnoreCase);
    }
}
