using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Graph.GraphRecipe;

namespace Aw.Sindri.Tests.Workflow.Graph;

/// <summary>Path order of states (spec §3.10, UI E-20): the first transition to an unvisited state is the main path, later ones branch, back edges are loops, terminals come last.</summary>
public sealed class StatePathOrderTests
{
    private static string Places(PathOrder order) =>
        string.Join(' ', order.States.Select(s => $"{s.State}={s.Order?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}{(s.BranchOf is null ? string.Empty : $"^{s.BranchOf}")}"));

    private static PathOrder Of(string initial, (string Name, RecipeState State)[] states, params RecipeTransition[] transitions) =>
        StatePathOrder.Of(GraphRecipe.Of(initial, states, transitions));

    private static PathOrder Shipped(string path) =>
        StatePathOrder.Of(RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path)).Recipe);

    [Fact, Trait("Lane", "unit")]
    public void A_straight_recipe_is_numbered_in_walk_order_with_the_terminal_last()
    {
        var order = Of("a", [("a", Active()), ("b", Active()), ("done", Terminal())], Go("a", "b"), Go("b", "done"));

        Assert.Equal("a=1 b=2 done=3", Places(order));
        Assert.Equal("path", order.OrderedBy);
        Assert.Empty(order.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_first_listed_transition_to_an_unvisited_state_is_the_main_path_and_a_later_one_is_a_branch()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("c", Active()), ("done", Terminal())],
            Go("a", "b"), Go("a", "c"), Go("b", "done"), Go("c", "done"));

        Assert.Equal("a=1 b=2 c=-^a done=3", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_reached_by_both_a_main_path_transition_and_a_branch_keeps_its_main_path_place()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("c", Active()), ("done", Terminal())],
            Go("a", "b"), Go("a", "c"), Go("b", "c"), Go("c", "done"));

        Assert.Equal("a=1 b=2 c=3 done=4", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void What_only_a_branch_reaches_is_folded_under_the_state_the_branch_leaves()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("c", Active()), ("d", Active()), ("done", Terminal())],
            Go("a", "b"), Go("a", "c"), Go("c", "d"), Go("b", "done"), Go("d", "done"));

        Assert.Equal("a=1 b=2 c=-^a d=-^a done=3", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void Terminal_states_come_last_in_the_order_the_walk_found_them()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("failed", Terminal("failed")), ("done", Terminal())],
            Go("a", "b"), Go("a", "failed"), Go("b", "done"));

        Assert.Equal("a=1 b=2 done=3 failed=4", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_back_to_a_state_on_the_walk_is_a_loop_edge_not_a_path_step()
    {
        var order = Of(
            "a",
            [("a", Active(loop: "rounds")), ("b", Active()), ("done", Terminal())],
            Go("a", "b"), Go("b", "done"), Go("b", "a"));

        Assert.Equal("a=1 b=2 done=3", Places(order));
        Assert.Equal([new PathLoop("rounds", "b", "a")], order.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_loop_edge_is_named_by_the_target_loop_else_the_looping_states_and_unnamed_when_none_declares_one()
    {
        var named = Of("a", [("a", Active()), ("b", Active(loop: "fix")), ("done", Terminal())], Go("a", "b"), Go("b", "a"), Go("b", "done"));
        var unnamed = Of("a", [("a", Active()), ("b", Active()), ("done", Terminal())], Go("a", "b"), Go("b", "a"), Go("b", "done"));

        Assert.Equal([new PathLoop("fix", "b", "a")], named.Loops);
        Assert.Equal([new PathLoop(Name: null, "b", "a")], unnamed.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_that_loops_to_itself_is_one_loop_edge()
    {
        var order = Of("a", [("a", Active(loop: "retry")), ("done", Terminal())], Go("a", "a"), Go("a", "done"));

        Assert.Equal("a=1 done=2", Places(order));
        Assert.Equal("path", order.OrderedBy);
        Assert.Equal([new PathLoop("retry", "a", "a")], order.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_listed_for_several_from_states_counts_for_each()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("done", Terminal())],
            Go("a", "b"), new RecipeTransition(["a", "b"], "done", "step.failed", When: null, [], [], [], []));

        Assert.Equal("a=1 b=2 done=3", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void Several_distinct_first_targets_from_initial_say_the_order_follows_the_transition_list()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("c", Active()), ("done", Terminal())],
            Go("a", "b"), Go("a", "c"), Go("b", "done"), Go("c", "done"));
        var single = Of("a", [("a", Active()), ("b", Active()), ("done", Terminal())], Go("a", "b"), Go("a", "b", "step.failed"), Go("b", "done"));

        Assert.Equal("transition-list", order.OrderedBy);
        Assert.Equal("path", single.OrderedBy);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_the_walk_never_reaches_is_listed_last_with_neither_an_order_nor_a_branch()
    {
        var order = Of("a", [("a", Active()), ("orphan", Active()), ("done", Terminal())], Go("a", "done"));

        Assert.Equal("a=1 done=2 orphan=-", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_to_an_undeclared_state_is_not_followed()
    {
        var order = Of("a", [("a", Active()), ("done", Terminal())], Go("a", "nowhere"), Go("a", "done"));

        Assert.Equal("a=1 done=2", Places(order));
        Assert.Equal("path", order.OrderedBy);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_terminal_target_listed_first_neither_makes_the_order_follow_the_list_nor_takes_the_main_path()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("failed", Terminal("failed")), ("done", Terminal())],
            Go("a", "failed"), Go("a", "b"), Go("b", "done"));

        Assert.Equal("path", order.OrderedBy);
        Assert.Equal("a=1 b=2 failed=3 done=4", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_shipped_item_recipe_is_ordered_by_path()
    {
        var order = Shipped("sindri/workflow.item.yaml");

        Assert.Equal("path", order.OrderedBy);
        Assert.Equal("triaged=1 startable=2 claimed=3 running=4 done=5 dropped=6", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_nested_loops_back_edge_is_named_by_the_target_loop_the_engine_counts_the_round_on()
    {
        var order = Of(
            "a",
            [("a", Active(loop: "outer")), ("b", Active(loop: "inner")), ("done", Terminal())],
            Go("a", "b"), Go("b", "a"), Go("b", "done"));

        Assert.Equal([new PathLoop("outer", "b", "a")], order.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_three_state_triage_recipe_is_desk_answer_done()
    {
        var order = Shipped("dev/triage-three-state.yaml");

        Assert.Equal("desk=1 answer=2 done=3", Places(order));
        Assert.Empty(order.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_desk_round_trip_recipe_is_export_import_done_with_the_loop_between_import_and_export()
    {
        var order = Shipped("dev/desk-round-trip.yaml");

        Assert.Equal("export=1 import=2 done=3", Places(order));
        Assert.Equal([new PathLoop("rounds", "import", "export")], order.Loops);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Every_state_of_a_shipped_recipe_has_an_order_or_a_branch(string path)
    {
        var order = Shipped(path);

        Assert.All(order.States, s => Assert.True(s.Order is not null || s.BranchOf is not null, $"{path}: {s.State}"));
    }


    /// <summary>Every shipped recipe's hash, pinned before DT-C changed how a produces entry is read (a bare key must keep writing as a string).</summary>
    public static IReadOnlyDictionary<string, string> PinnedHashes { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["sindri/workflow.phase.yaml"] = "sha256:253ce29641ef0023f8d298a7b9ed3bbedb18be0b90f83ce5da4342b5298625f4",
        ["sindri/workflow.pr-babysitter.yaml"] = "sha256:03bcd8c465f70a112f3f565933918a585f6384caa92b5788639bde835f574211",
        ["sindri/workflow.build.yaml"] = "sha256:575430facdb0cb6187380210f609390c457b24119f0eb7173a16404dc832a284",
        ["sindri/workflow.prove.yaml"] = "sha256:d0421eb0db5c89641e4dce65ba96bfc3943391692e9083c603026d8bdd2d50e0",
        ["sindri/workflow.item.yaml"] = "sha256:85ecaff4781ee6cfd458a983861cf04a66228fde579c95722b03b353a3532d45",
        ["sindri/workflow.step.yaml"] = "sha256:1c1173a162a77736c33620e985d6d75473cf3755963adc5c46eb9049434f5276",
        ["sindri/workflow.docs-sync.yaml"] = "sha256:11b60c0a34d527bfd833238f5885173f1f3354631b9f5d011ec45be710190a23",
        ["sindri/workflow.design.yaml"] = "sha256:bded5e42ff0f480965d3299b0b9862338fbb5679b3f83ed686c3c07b9c396c30",
        ["sindri/workflow.plan.yaml"] = "sha256:a93f3b464364a4e7287ab18568e2d9e9c3eb34a505a25d54c73278e38f5abf21",
        ["sindri/workflow.evidence-all.yaml"] = "sha256:74202f57161dc2bfd1d4e75c104df71f13124f965f9a2c8a47db5064b82a8095",
        ["sindri/workflow.evidence-api.yaml"] = "sha256:42e65ca9078346f3504e45aeee34fea77a5554fcab6f86e7eb4b1868be829b10",
        ["sindri/workflow.evidence-db.yaml"] = "sha256:e10eb14d478226803ee8591a2ac65445f9a8d1bb1580f4a1eaf15449bed3a057",
        ["sindri/workflow.evidence-other.yaml"] = "sha256:d2c087e43a6da3be60e1886889450c27a805ef0b98c360b28e9a47dc5dc710c6",
        ["sindri/workflow.evidence-ui.yaml"] = "sha256:52846402d20666af36ed650493e533bc9014b56e8cb950418a5df19db49a29ae",
        ["sindri/workflow.evidence.yaml"] = "sha256:31e442a2a69841173c2bb78ba5798636d7de17f9a055c2e240876342fb2dfe20",
        ["sindri/workflow.hand-test.yaml"] = "sha256:f703998af368cba5efce2b137f6ec27ce957e79ff1fdb6ca85f29776e3a92832",
        ["sindri/workflow.idea-to-brief.yaml"] = "sha256:5089f20c728211996253d6f308333d26e6dc19a01824edb2ca1704bea84f592a",
        ["sindri/workflow.implement.yaml"] = "sha256:7c8810ab0f87bae30c124a07ebff4a6cbf3ee2b014e8f5048df8b24985601a25",
        ["sindri/workflow.investigate.yaml"] = "sha256:1960b6b2db89ffae4aac34ec98b3ea757a247fd871eb71b7b229e7f8080fde77",
        ["sindri/workflow.line.yaml"] = "sha256:251e34be8370d52c8b70e57ff5f52bbac5a64123ca04c9eccebe8d7a597eeea2",
        ["sindri/workflow.report.yaml"] = "sha256:864f58986b4308e16c42ba7d36f751949401a496cb9f12d93d8568a1dac4c093",
        ["sindri/workflow.reproduce.yaml"] = "sha256:690055ffdec765ac25dd7d32af75fb5036480e36e9cb2cc3397b375828a25518",
        ["sindri/workflow.review-loop.yaml"] = "sha256:0ebe6a73b6c9539ab23d93e762e3549a859db92cb3568faa16cca43de369f3d5",
        ["sindri/workflow.scoping.yaml"] = "sha256:be8800481ef2efe00f1ae951adee19bed67d316d1cae646fa3e618813cd25377",
        ["sindri/workflow.ship.yaml"] = "sha256:98d3d07fd83d23bd5a05fcdb1887aeb67d9de826451af8a8098b4098d4abd8c4",
        ["sindri/workflow.verify.yaml"] = "sha256:3f1aeae387cf74c50a02eb397f88791abb93d9bd43bc92accf7025b70e82f696",
        ["dev/desk-round-trip.yaml"] = "sha256:459f0bb87b5ab29e00fe744561f13e95e8a5819debcd065a1c52b0ad3aba27ac",
        ["dev/incident-hold.yaml"] = "sha256:fe5798176e157e8571c6f6accf00aa3dc36b56fe5836f37333af6b936a293ad8",
        ["dev/triage-three-state.yaml"] = "sha256:94c0c28e21ca37b9f9ad16233e33c9620b70e940c0fc19f3fb14cc9dfc51f002",
        ["dev/workflow.dev-child-outputs.yaml"] = "sha256:f66693ba9983b2bd047afb4b8398c438bd033adbe04ccea31d0964b27096f2a2",
        ["dev/workflow.dev-child-summary.yaml"] = "sha256:fe3a3d60a4a5d9c9f94b5feb9bca7f7a8b44fbbf04aa2d765e640b3a9ff43c57",
        ["dev/workflow.dev-evidence-route.yaml"] = "sha256:49b8827fdf75f75694807ae34c1df7395563cc969997ac31f203023bf2872c37",
        ["dev/workflow.dev-notify.yaml"] = "sha256:4bb9f16cd6f6a5fd871f87d70d0a87da82bf7a1967779cfe295d4d7b6fe1dc8d",
        ["dev/workflow.dev-timeout.yaml"] = "sha256:ab732e4c4a955d653104d3639274967ad506b97842a4b2d57d07657b0347cf9c",
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Pinned))]
    public void The_path_order_is_not_hashed_so_every_shipped_recipe_keeps_its_hash(string path, string hash)
    {
        var document = RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path));
        var json = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(document.Recipe, SliceHarness.Json));

        Assert.Equal(hash, document.Hash.Value);
        Assert.DoesNotContain("orderedBy", json, StringComparison.Ordinal);
        Assert.DoesNotContain("branchOf", json, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_shipped_recipe_has_a_pinned_hash() =>
        Assert.Empty(ShippedRecipes.Recipes.Except(PinnedHashes.Keys, StringComparer.Ordinal));

    public static TheoryData<string, string> Pinned => [.. PinnedHashes.Select(p => (p.Key, p.Value))];

    public static TheoryData<string> Recipes => [.. ShippedRecipes.Recipes];
}
