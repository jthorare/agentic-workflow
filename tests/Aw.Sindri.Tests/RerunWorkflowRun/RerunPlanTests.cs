using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Rerun;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.RerunWorkflowRun;

/// <summary>
/// The pure part of 'Re-run from here' (spec §7.3): which steps are after the chosen one, which come before it, which the re-run takes from the source run,
/// and which branch a taken step took, read from its own stored row (a condition's result, an ask_owner's answer), never guessed from the steps after it.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RerunPlanTests
{
    private const string True = "{\"result\":true}";

    private static NodeId N(string id) => NodeId.From(id);

    private static NodeRun Row(string node, NodeRunStatus status, string? output = null) =>
        new(WorkflowRunId.Of(WorkflowSlug.From("plan"), 1, 1), N(node), 0, status, Input: null, output, Truncated: false, Error: null, Usage: null);

    private static GraphNode Node(string id, string type = "http") => new(N(id), type, Title: null, []);

    private static GraphEdge Edge(string from, string to, string? output = null) => new(N(from), output, N(to));

    private static GraphSpec Graph(GraphNode[] nodes, GraphEdge[] edges) => new(Version: 1, Name: "x", Description: null, Concurrency: null, Resources: null, nodes, edges);

    // t -> a -(true)-> b -> c ; a -(false)-> d
    private static GraphSpec Spec() => Graph([Node("t", "manual"), Node("a", "condition"), Node("b"), Node("c"), Node("d")],
        [Edge("t", "a"), Edge("a", "b", "true"), Edge("b", "c"), Edge("a", "d", "false")]);

    // t -> check -(true)-> post_ok, check -(false)-> page_oncall, t -> fetch (the review's graph)
    private static GraphSpec Alert() => Graph([Node("t", "manual"), Node("check", "condition"), Node("fetch"), Node("post_ok"), Node("page_oncall")],
        [Edge("t", "check"), Edge("check", "post_ok", "true"), Edge("check", "page_oncall", "false"), Edge("t", "fetch")]);

    private static string[] Restored(GraphSpec spec, string from, params NodeRun[] rows) => [.. RerunPlan.Restored(spec, N(from), rows).Select(static i => i.Value)];

    [Fact]
    public void FromHere_is_the_node_and_everything_it_reaches_in_spec_order()
    {
        Assert.Equal(["b", "c"], RerunPlan.FromHere(Spec(), N("b")).Select(static i => i.Value), StringComparer.Ordinal);
        Assert.Equal(["a", "b", "c", "d"], RerunPlan.FromHere(Spec(), N("a")).Select(static i => i.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void Before_is_what_the_node_reads_from_and_not_the_node_or_a_side_branch()
    {
        Assert.Equal(["t", "a"], RerunPlan.Before(Spec(), N("b")).Select(static i => i.Value), StringComparer.Ordinal);
        Assert.Empty(RerunPlan.Before(Spec(), N("t")));
    }

    [Fact]
    public void A_step_skipped_on_the_branch_a_ran_step_did_not_take_is_restored()
    {
        // a took 'true' and b failed, so d is dead; c was skipped because the run halted, but it is after b anyway
        var restored = Restored(Spec(), "b", Row("t", NodeRunStatus.Succeeded), Row("a", NodeRunStatus.Succeeded, True), Row("b", NodeRunStatus.Failed),
            Row("c", NodeRunStatus.Skipped), Row("d", NodeRunStatus.Skipped));

        Assert.Equal(["t", "a", "d"], restored);
    }

    // why: this fact used to pin "no step after it ran, so every branch counts as taken", which ran d although a said true (#3204 review); the branch now comes from a's own row
    [Fact]
    public void A_branching_step_whose_steps_never_ran_takes_the_branch_its_row_says()
    {
        var restored = Restored(Spec(), "b", Row("t", NodeRunStatus.Succeeded), Row("a", NodeRunStatus.Succeeded, True), Row("b", NodeRunStatus.Skipped),
            Row("c", NodeRunStatus.Skipped), Row("d", NodeRunStatus.Skipped));

        Assert.Equal(["t", "a", "d"], restored);
    }

    [Fact]
    public void A_branching_step_whose_row_does_not_say_its_branch_runs_again()
    {
        var restored = Restored(Spec(), "b", Row("t", NodeRunStatus.Succeeded), Row("a", NodeRunStatus.Succeeded, "{\"other\":1}"), Row("b", NodeRunStatus.Skipped),
            Row("c", NodeRunStatus.Skipped), Row("d", NodeRunStatus.Skipped));

        Assert.Equal(["t"], restored);
    }

    // The review's case: check said true, fetch failed first, and the halt skipped post_ok. A re-run from fetch or post_ok runs post_ok and keeps page_oncall skipped.
    [Theory]
    [InlineData("fetch")]
    [InlineData("post_ok")]
    public void A_halted_branch_step_runs_again_and_the_branch_its_condition_did_not_take_stays_skipped(string from)
    {
        var restored = Restored(Alert(), from, Row("t", NodeRunStatus.Succeeded), Row("check", NodeRunStatus.Succeeded, True), Row("fetch", NodeRunStatus.Failed),
            Row("post_ok", NodeRunStatus.Skipped), Row("page_oncall", NodeRunStatus.Skipped));

        Assert.Equal(["t", "check", "page_oncall"], restored);
    }

    [Fact]
    public void A_step_after_a_step_that_runs_again_runs_again_too()
    {
        var restored = Restored(Spec(), "c", Row("t", NodeRunStatus.Succeeded), Row("a", NodeRunStatus.Failed), Row("b", NodeRunStatus.Succeeded),
            Row("c", NodeRunStatus.Succeeded), Row("d", NodeRunStatus.Skipped));

        Assert.Equal(["t"], restored);
    }

    [Theory]
    [InlineData("{\"result\":true}", "true")]
    [InlineData("{\"result\":false}", "false")]
    public void A_condition_took_the_branch_of_its_stored_result(string output, string branch)
        => Assert.Equal([branch], RerunPlan.Branches(Node("a", "condition"), [Row("a", NodeRunStatus.Succeeded, output)])!, StringComparer.Ordinal);

    [Theory]
    [InlineData("{\"answer\":\"yes\",\"answeredBy\":\"human:app\"}", "answered")]
    [InlineData("{\"answer\":\"\",\"answeredBy\":\"timeout\"}", "timeout")]
    public void An_ask_owner_took_answered_or_timeout_by_who_answered(string output, string branch)
        => Assert.Equal([branch], RerunPlan.Branches(Node("ask", "ask_owner"), [Row("ask", NodeRunStatus.Succeeded, output)])!, StringComparer.Ordinal);

    [Theory]
    [InlineData("condition", null)]
    [InlineData("condition", "{\"result\":\"yes\"}")]
    [InlineData("ask_owner", "{\"answer\":\"yes\"}")]
    [InlineData("condition", "not json")]
    public void A_branching_step_whose_row_does_not_say_its_branch_has_no_branch(string type, string? output)
        => Assert.Null(RerunPlan.Branches(Node("a", type), [Row("a", NodeRunStatus.Succeeded, output)]));

    [Fact]
    public void Any_other_step_took_out()
        => Assert.Equal(["out"], RerunPlan.Branches(Node("a"), [Row("a", NodeRunStatus.Succeeded, "{\"status\":200}")])!, StringComparer.Ordinal);

    [Fact]
    public void A_re_run_from_a_step_on_a_branch_the_source_did_not_take_takes_that_branch_on_purpose()
    {
        Assert.Equal(["true", "false"], RerunPlan.WithChosen(Spec(), N("a"), ["true"], N("d")), StringComparer.Ordinal);
        Assert.Equal(["true"], RerunPlan.WithChosen(Spec(), N("a"), ["true"], N("c")), StringComparer.Ordinal);
    }

    [Fact]
    public void A_skipped_step_took_no_branch()
        => Assert.Empty(RerunPlan.Branches(Node("a", "condition"), [Row("a", NodeRunStatus.Skipped)])!);
}
