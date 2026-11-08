using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Tests.Events;

/// <summary>The frame a ledger delta shows names who runs the open run, so a desk task reads apart from an agent run (API P11 U1).</summary>
public sealed class WorkflowEventDeltaTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(StepRuntime.Desk)]
    [InlineData(StepRuntime.Agent)]
    public void Delta_frame_names_the_runtime_of_an_open_run(StepRuntime runtime)
    {
        var frame = new Frame("produce", 3, new OpenRun(RunToken.For(3, 1), runtime, Launch: null, Task: null), Child: null);

        var delta = WorkflowEventDelta.Of(Delta(frame));

        Assert.Equal(new FrameSummary("produce", 3, "3.1", Child: null, runtime), delta.Frame);
    }

    [Fact, Trait("Lane", "unit")]
    public void Delta_frame_omits_runtime_when_no_run_is_open()
    {
        var delta = WorkflowEventDelta.Of(Delta(new Frame("wait", 2, Run: null, Child: null)));

        Assert.Null(delta.Frame!.Runtime);
        Assert.DoesNotContain("\"runtime\"", System.Text.Json.JsonSerializer.Serialize(delta.Frame, SliceHarness.Json), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Delta_frame_with_a_runtime_serializes_it_as_the_word_and_reads_it_back()
    {
        var frame = new FrameSummary("produce", 3, "3.1", Child: null, StepRuntime.Desk);

        var json = System.Text.Json.JsonSerializer.Serialize(frame, SliceHarness.Json);

        Assert.Contains("\"runtime\":\"desk\"", json, StringComparison.Ordinal);
        Assert.Equal(frame, System.Text.Json.JsonSerializer.Deserialize<FrameSummary>(json, SliceHarness.Json));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_stored_frame_from_before_the_field_reads_with_no_runtime()
    {
        var stored = System.Text.Json.JsonSerializer.Deserialize<FrameSummary>("{\"state\":\"produce\",\"entry\":3,\"run\":\"3.1\",\"child\":null}", SliceHarness.Json);

        Assert.Equal(new FrameSummary("produce", 3, "3.1", Child: null, Runtime: null), stored);
    }

    private static InstanceDelta Delta(Frame frame) =>
        new(frame, Suspension: null, PendingPark: null, Loops: null, Spent: null, Facts: null, Outcome: null);
}
