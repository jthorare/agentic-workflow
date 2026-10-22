using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.Tests.Run;

/// <summary>
/// The run canvas's graph from what is served now (F3 spec 3 §4): the engine serves no transitions yet, so the main chain comes from
/// the path order, a loop from the recipe's loops, only the end that was reached is drawn, and no pill text is invented.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunFallbackTests
{
    private static readonly Dictionary<string, InstanceDetail> _none = new(StringComparer.Ordinal);

    private static Dictionary<string, string> Chips(string stage, string word) => new(StringComparer.Ordinal) { [stage] = word };

    private static Dictionary<string, InstanceDetail> Read(string id, InstanceDetail child) => new(StringComparer.Ordinal) { [id] = child };

    private static RunGraph Build(InstanceDetail line, IReadOnlyDictionary<string, InstanceDetail>? children = null) =>
        RunGraphBuilder.Build(line, RunFixtures.LineRecipe(), children ?? _none);

    private static RunEdge EdgeOf(RunGraph graph, string key) => Assert.Single(graph.Edges, e => e.Key == key);

    private static RunNode NodeOf(RunGraph graph, string id) => Assert.Single(graph.Nodes, n => n.Id == id);

    [Fact]
    public void Missing_transitions_draw_the_main_chain_and_the_reached_end()
    {
        var graph = Build(RunFixtures.Line("done"));

        Assert.Equal(["scoping", "design", "plan", "phase", "done"], graph.Nodes.Select(n => n.Id), StringComparer.Ordinal);
        Assert.Equal([RunNodeKind.Stage, RunNodeKind.Stage, RunNodeKind.Stage, RunNodeKind.Stage, RunNodeKind.End], graph.Nodes.Select(n => n.Kind));
        Assert.Equal(["scoping>design", "design>plan", "plan>phase", "phase>done"], graph.Edges.Select(e => e.Key), StringComparer.Ordinal);
        Assert.Equal(RunEdgeKind.End, EdgeOf(graph, "phase>done").Kind);
        Assert.Equal("resolved", EdgeOf(graph, "phase>done").Outcome);
        Assert.All(graph.Edges, e => Assert.Null(e.Label));
        Assert.False(graph.BranchesServed);
        Assert.Equal(RunNodeState.Done, NodeOf(graph, "done").State);
    }

    [Fact]
    public void A_run_still_going_draws_no_end_card_and_leaves_the_edge_to_the_next_stage_dim()
    {
        var graph = Build(RunFixtures.Line("plan"));

        Assert.Equal(["scoping", "design", "plan", "phase"], graph.Nodes.Select(n => n.Id), StringComparer.Ordinal);
        Assert.Equal(
            [RunNodeState.Done, RunNodeState.Done, RunNodeState.Running, RunNodeState.NotStarted],
            graph.Nodes.Select(n => n.State));
        Assert.Equal(EdgeTone.Normal, EdgeOf(graph, "scoping>design").Tone);
        Assert.Equal(EdgeTone.Active, EdgeOf(graph, "design>plan").Tone);
        Assert.Equal(EdgeTone.Dim, EdgeOf(graph, "plan>phase").Tone);
    }

    [Fact]
    public void Stages_that_have_not_started_are_joined_by_normal_edges()
    {
        var graph = Build(RunFixtures.Line("scoping"));

        Assert.Equal(EdgeTone.Dim, EdgeOf(graph, "scoping>design").Tone);
        Assert.Equal(EdgeTone.Normal, EdgeOf(graph, "design>plan").Tone);
        Assert.Equal(EdgeTone.Normal, EdgeOf(graph, "plan>phase").Tone);
    }

    [Fact]
    public void A_loop_the_run_went_through_draws_its_card_and_a_dim_edge_with_the_loop_pill_from_the_recipe()
    {
        var line = RunFixtures.Line("plan");
        var reached = line with { States = [.. line.States.Select(c => c.State == "redesign" ? c with { Glyph = StateGlyph.Done } : c)] };

        var graph = Build(reached);

        Assert.Equal(RunNodeKind.Side, NodeOf(graph, "redesign").Kind);
        var loop = EdgeOf(graph, "redesign>plan");
        Assert.Equal((RunEdgeKind.Loop, EdgeTone.Dim, "again, up to 2 times"), (loop.Kind, loop.Tone, loop.Label));
    }

    [Fact]
    public void A_loop_card_the_run_never_reached_is_not_drawn()
    {
        var graph = Build(RunFixtures.Line("plan"));

        Assert.DoesNotContain(graph.Nodes, n => n.Id == "redesign");
        Assert.DoesNotContain(graph.Edges, e => e.Kind == RunEdgeKind.Loop);
    }

    [Fact]
    public void A_stage_is_named_by_its_state_and_its_workflow_by_the_served_name_else_the_id_in_words()
    {
        var line = RunFixtures.Line("plan");
        var unnamed = line with { Children = [.. line.Children.Select(g => g.State == "scoping" ? g with { DisplayName = null } : g)] };

        var graph = Build(unnamed);

        Assert.Equal(("Scoping", "Scoping"), (NodeOf(graph, "scoping").Name, NodeOf(graph, "scoping").WorkflowName));
        Assert.Equal("Design", NodeOf(graph, "design").WorkflowName);
    }

    [Fact]
    public void A_recipe_that_is_not_read_still_draws_the_chain_and_no_loop()
    {
        var graph = RunGraphBuilder.Build(RunFixtures.Line("plan"), recipe: null, _none);

        Assert.Equal(3, graph.Edges.Count);
    }

    [Fact]
    public void A_stage_shows_its_first_child_that_is_not_done()
    {
        var line = RunFixtures.Line("plan");
        var rows = new[] { RunFixtures.Row("WFI-a", "plan", "DONE"), RunFixtures.Row("WFI-b", "plan", "RUNNING"), RunFixtures.Row("WFI-c", "plan", "QUEUED") };
        var many = line with { Children = [.. line.Children.Select(g => g.State == "plan" ? g with { Rows = rows } : g)] };

        var graph = Build(many);

        Assert.Equal(("WFI-b", RunNodeState.Running), (NodeOf(graph, "plan").ChildId, NodeOf(graph, "plan").State));
    }

    [Fact]
    public void A_stage_takes_its_state_from_its_childs_chip()
    {
        Assert.Equal(RunNodeState.Waiting, NodeOf(Build(RunFixtures.Line("plan", Chips("plan", "DECISION"))), "plan").State);
        Assert.Equal(RunNodeState.Paused, NodeOf(Build(RunFixtures.Line("plan", Chips("plan", "PARKED"))), "plan").State);
        Assert.Equal(RunNodeState.TookOver, NodeOf(Build(RunFixtures.Line("plan", Chips("plan", "WITH YOU"))), "plan").State);
        Assert.Equal(EdgeTone.Active, EdgeOf(Build(RunFixtures.Line("plan", Chips("plan", "DECISION"))), "design>plan").Tone);
    }

    [Fact]
    public void A_failed_stage_ends_in_a_red_edge_to_the_end_card_that_reads_failed()
    {
        var graph = Build(RunFixtures.Line("stuck", Chips("phase", "FAILED"), outcome: "failed"));

        Assert.Equal(RunNodeState.Failed, NodeOf(graph, "phase").State);
        Assert.Equal(RunNodeState.Failed, NodeOf(graph, "stuck").State);
        Assert.Equal(EdgeTone.Failed, EdgeOf(graph, "phase>stuck").Tone);
        Assert.Equal(EdgeTone.Normal, EdgeOf(graph, "plan>phase").Tone);
    }

    [Fact]
    public void A_run_that_didnt_work_out_or_was_cancelled_reaches_its_end_by_a_solid_edge()
    {
        var unresolved = Build(RunFixtures.Line("stuck", Chips("phase", "DIDN'T WORK OUT"), outcome: "unresolved"));
        var cancelled = Build(RunFixtures.Line("dropped", Chips("design", "CANCELLED"), outcome: "cancelled"));

        Assert.Equal((RunNodeState.DidntWorkOut, RunNodeState.DidntWorkOut, EdgeTone.Normal), (NodeOf(unresolved, "phase").State, NodeOf(unresolved, "stuck").State, EdgeOf(unresolved, "phase>stuck").Tone));
        Assert.Equal((RunNodeState.Cancelled, EdgeTone.Normal), (NodeOf(cancelled, "dropped").State, EdgeOf(cancelled, "phase>dropped").Tone));
    }

    [Fact]
    public void A_stages_steps_come_from_its_child_with_who_does_them()
    {
        var child = RunFixtures.Child("plan", ("draft", StateGlyph.Done, false), ("review-it", StateGlyph.Active, true), ("ship", StateGlyph.Pending, false));

        var steps = NodeOf(Build(RunFixtures.Line("plan"), Read("WFI-plan", child)), "plan").Steps;

        Assert.Equal(["Draft", "Review It", "Ship"], steps.Select(s => s.Name), StringComparer.Ordinal);
        Assert.Equal([RunNodeState.Done, RunNodeState.Waiting, RunNodeState.NotStarted], steps.Select(s => s.State));
        Assert.Equal([RunStepKind.Unknown, RunStepKind.You, RunStepKind.Unknown], steps.Select(s => s.Kind));
        Assert.DoesNotContain(steps, s => s.Id == "done");
    }

    [Fact]
    public void An_agent_step_run_marks_its_step_as_an_agents()
    {
        var child = RunFixtures.Child("plan", ("draft", StateGlyph.Active, false));
        var run = new StepRun("draft.1", "draft", Attempt: 1, MaxAttempts: null, "running", "agent", DateTimeOffset.UnixEpoch, EndedAt: null, DurationMs: null, Outcome: null, Tokens: null, Actor: null);

        var steps = NodeOf(Build(RunFixtures.Line("plan"), Read("WFI-plan", child with { StepRuns = [run] })), "plan").Steps;

        Assert.Equal((RunNodeState.Running, RunStepKind.Agent), (steps[0].State, steps[0].Kind));
    }

    [Fact]
    public void A_stage_whose_child_is_not_read_lists_no_steps() =>
        Assert.Empty(NodeOf(Build(RunFixtures.Line("plan")), "plan").Steps);
}
