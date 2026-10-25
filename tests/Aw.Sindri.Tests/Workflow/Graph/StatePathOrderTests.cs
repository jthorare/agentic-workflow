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
        ["sindri/workflow.phase.yaml"] = "sha256:102a81094b9466a36f16fa8d3a240334c53bfa51404f0e3f26808c0948f8210c",
        ["sindri/workflow.pr-babysitter.yaml"] = "sha256:96efc9795aa69543bc6f56a8bfe7f67ab5629363e3b70a33f47a11fcb158f582",
        ["sindri/workflow.build.yaml"] = "sha256:493c71ca3f8f398d9ade2a36162dee2e464ceb5103b2d6f1d39271648087b3d8",
        ["sindri/workflow.prove.yaml"] = "sha256:07ef542cd2659b6d8189f5b7c768fc9d2033db7a3202b4dede6c29d6a1dfa340",
        ["sindri/workflow.item.yaml"] = "sha256:fbd360859893f44e3d8c2bd83de7ee8edd760fb66f5541c99f5edb0e89406176",
        ["sindri/workflow.step.yaml"] = "sha256:f511ce1fc529b5b471731b4023a1f07774af2f9a8836344fd5818f5aef644784",
        ["sindri/workflow.prove-recheck.yaml"] = "sha256:82e07268000bb8a5ef0a35c7dab9db88152c7915dfbf20104369be9330aa34c8",
        ["sindri/workflow.docs-sync.yaml"] = "sha256:80c894622a5d7a3202b1e74d0eb3b273348d0a3b8a0d424179017361942e3e2f",
        ["sindri/workflow.design.yaml"] = "sha256:bda3c74fb15c425c4f22113c0c46e8af51cc5c12dbba3cc07614b878efad3b50",
        ["sindri/workflow.plan.yaml"] = "sha256:b3fc2d83bba2a09d1be9d5fed23ba19c389115edbeb0bc7c17ca9999b96e1903",
        ["sindri/workflow.evidence-all.yaml"] = "sha256:20e8eca114d1008e6576a258c277a8877f96f0ac3bfcf4f1dfbeb9f825f50ff6",
        ["sindri/workflow.evidence-api.yaml"] = "sha256:04f2c69410ddf93c837425b639fdf1cd2e83e239151d5941945d91a8e527064f",
        ["sindri/workflow.evidence-db.yaml"] = "sha256:1823b8173f00029dcab5c78030ba6290fe8a794124cfbd6a783107049046ea9a",
        ["sindri/workflow.evidence-desktop.yaml"] = "sha256:df9859d2a30d263c007d73a46db42bd6e78a65e5acff211f921341b44a22a564",
        ["sindri/workflow.evidence-other.yaml"] = "sha256:8f4acda2302eb1e50f1a0087f163a2fda6c2d60674aeb34f5884112a032fbdb2",
        ["sindri/workflow.evidence-ui.yaml"] = "sha256:f8aaab721054a30287c5c2591316c421217b55aa22e29cb089821ad0d9a678ad",
        ["sindri/workflow.evidence.yaml"] = "sha256:72a2fb17f07ae2afe0a2076505762463353cf251259ce3da92ded8e41da9b778",
        ["sindri/workflow.hand-test.yaml"] = "sha256:b0ac4f18f6fb62d4cd6f240dac0fdea8d7f68b18a2ac5c54a83a9ed71be937ef",
        ["sindri/workflow.idea-to-brief.yaml"] = "sha256:a461f00122ba719324a8efa2455ec10068514694de609c15eb6a2a7d9017fb0c",
        ["sindri/workflow.implement.yaml"] = "sha256:11ce9896e2acb52043caef2f830315bff133f0b5ae8bdc332e7d44337f2477c9",
        ["sindri/workflow.investigate.yaml"] = "sha256:d5063e0f4dc63c3e9cc7f82602268fdbfca3264919204b42b9ba203a817aba35",
        ["sindri/workflow.line.yaml"] = "sha256:286e6840046be63017e006629d0437a8bed94cf77cbb0d989342a299988cb6ed",
        ["sindri/workflow.report.yaml"] = "sha256:8871919bc3996d208933a980c94099a0c81a635a469b325183f19f69c930ee2a",
        ["sindri/workflow.reproduce.yaml"] = "sha256:489ee28ed7feb3db4ca30fcae6fd3177004386343c8d787d7a0b653dd0b9d26d",
        ["sindri/workflow.review-loop.yaml"] = "sha256:39ac35e98ea8e6da62b7412eba50f59a5f41ae64ed1d763aa8738d2a5feace86",
        ["sindri/workflow.scoping.yaml"] = "sha256:f72ebcc9a47af7022611760a6a13290abf7b4434a622063eb18ebb656b0e7ec1",
        ["sindri/workflow.ship.yaml"] = "sha256:a8b11e85fed45fce4f505d08178e07377f26a782c7224beae0f9d091b582f3e8",
        ["sindri/workflow.verify.yaml"] = "sha256:83d97974672c63b53bd95eded001f102ab710fbd6a89b1006461302eff7175dc",
        ["dev/desk-round-trip.yaml"] = "sha256:22e3d173c3030036026aa70ae192f0bbe64b5135960ec38d7f05976443595357",
        ["dev/incident-hold.yaml"] = "sha256:fae531ac58b3c3990c51a125548aa83e832f4d0dd77231d272e860b067b7cda1",
        ["dev/triage-three-state.yaml"] = "sha256:b77152c8613ea86d8a64fdf9e9a140c23b1c9352fc06fcebe5c1254ca5e2ad93",
        ["dev/workflow.dev-child-outputs.yaml"] = "sha256:6f9b501303c24b0b52aff07ad626d2307bff3f1d53beb2064fb664cb1bd6b1a6",
        ["dev/workflow.dev-child-summary.yaml"] = "sha256:38927532c385acbeba52f99071a1db63b3c6a3321e326ad146fc3a884ced2ef5",
        ["dev/workflow.dev-evidence-route.yaml"] = "sha256:0c01b4db81f68c72fa87b9a5b42678085c733a3af454934065e6d3fa38e84faf",
        ["dev/workflow.dev-notify.yaml"] = "sha256:f43681b4e51f390e9eea05af3437a9e43fa6b04b0b4a86a26415bed10a17b125",
        ["dev/workflow.dev-timeout.yaml"] = "sha256:62594aabbd24f940d3cfadd383e9325100f8a9208184dcb885c7721ed70643b9",
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
