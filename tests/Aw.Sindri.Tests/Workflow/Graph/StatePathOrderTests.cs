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
        ["sindri/workflow.phase.yaml"] = "sha256:c2c9e93d3f1b4bf0b4755df18ba9659c2c38edf43f67d976870541b580ac9924",
        ["sindri/workflow.pr-babysitter.yaml"] = "sha256:d084a491790425501eb252944e908607225f51b06737e2b047eef213c20b2e0a",
        ["sindri/workflow.build.yaml"] = "sha256:3e7e6f4b001fe3dc42f9ec9b3343a05a56e38f6745e096e6a707b67f27faa56f",
        ["sindri/workflow.prove.yaml"] = "sha256:937abf38fb5f2ce7380abe7b571aee3f5a646e083971f228171fb9914676a7d4",
        ["sindri/workflow.item.yaml"] = "sha256:fbd360859893f44e3d8c2bd83de7ee8edd760fb66f5541c99f5edb0e89406176",
        ["sindri/workflow.step.yaml"] = "sha256:d39861bd12222fcc4b74432567eabfd7b58660c4b8d714be71528b6a1353ba2a",
        ["sindri/workflow.prove-recheck.yaml"] = "sha256:13ca707542f1b797595f7cd437ef09265baddbdbd70e104cda51a2a79324ad97",
        ["sindri/workflow.docs-sync.yaml"] = "sha256:571d9975c6f0d996cb425a1589488f13b7519ba89a9b72d0f7847ee6a65817eb",
        ["sindri/workflow.design.yaml"] = "sha256:2472ccdebd98923d9a5d8ba355cc7e01329e9d47a2b667829b107096fe977a51",
        ["sindri/workflow.plan.yaml"] = "sha256:f9e3d3c2172473323c5ad31973fea2b103add1e9fd8b49021789d0d872d032a9",
        ["sindri/workflow.evidence-all.yaml"] = "sha256:800188dce894dc0ce40ae35edfb37db1d7146972f0ee5223bf5b35c4050960b0",
        ["sindri/workflow.evidence-api.yaml"] = "sha256:4c715ff044d7445c3c325ff778732af0e20f4aa3b081d31ea17fdabdd5b40fc0",
        ["sindri/workflow.evidence-db.yaml"] = "sha256:c44cfe42131b1ef56589e95b402c07d8257f0566086953b958bb2de87ae5e37e",
        ["sindri/workflow.evidence-other.yaml"] = "sha256:8f4acda2302eb1e50f1a0087f163a2fda6c2d60674aeb34f5884112a032fbdb2",
        ["sindri/workflow.evidence-ui.yaml"] = "sha256:9628f0bb33230de9e79da853c56f695d9143c4f48e572a54ff31b1c2e7cc008e",
        ["sindri/workflow.evidence.yaml"] = "sha256:9975b084a6552996c3a432c638aa5b4289691eae8dcfc121d28210b73c7ec74a",
        ["sindri/workflow.hand-test.yaml"] = "sha256:45df2e5cacaf8cd142fe0dc38749b561e3a0a8517e08cbbd5fe691eed5c8417e",
        ["sindri/workflow.idea-to-brief.yaml"] = "sha256:4a6f9408961e5ae2569844a8a18b3ba13b0e1b65e5e7a73d4eb21d2160559350",
        ["sindri/workflow.implement.yaml"] = "sha256:6512ecd4064d55008fab81abf547f99b277ac3948619cea5e7bc19994495fc26",
        ["sindri/workflow.investigate.yaml"] = "sha256:b1fd802058b01d3a811513bdc91302b6ce3fdb7664b434cf3f7fe90be8e25df4",
        ["sindri/workflow.line.yaml"] = "sha256:b65890aa3b446c422dd5b5ac86b6d1e80ac112818a5a49e425e0432b7478ad5e",
        ["sindri/workflow.report.yaml"] = "sha256:8871919bc3996d208933a980c94099a0c81a635a469b325183f19f69c930ee2a",
        ["sindri/workflow.reproduce.yaml"] = "sha256:6fd78a6cb6df1630470cfb8dc53c4424986bf578433bad20af1113e54801ed75",
        ["sindri/workflow.review-loop.yaml"] = "sha256:522e7e223f3d845d220b7d91d20951c848ef15cda1c700b9e923f619d06bb584",
        ["sindri/workflow.scoping.yaml"] = "sha256:ef152e04b7cd48eb8907ce7dab2c5aa2d408d1cd9536c2b78b7983aa51ebd40d",
        ["sindri/workflow.ship.yaml"] = "sha256:4dec19c834ea8c2d0d5a8c7c3135440665bd3a6d51bcefb478d74f64e2861023",
        ["sindri/workflow.verify.yaml"] = "sha256:0d2648f7de735d4bde51db33d7f659ffbde2106d2062d8b0261b0e41507ee13a",
        ["dev/desk-round-trip.yaml"] = "sha256:22e3d173c3030036026aa70ae192f0bbe64b5135960ec38d7f05976443595357",
        ["dev/incident-hold.yaml"] = "sha256:fae531ac58b3c3990c51a125548aa83e832f4d0dd77231d272e860b067b7cda1",
        ["dev/triage-three-state.yaml"] = "sha256:b77152c8613ea86d8a64fdf9e9a140c23b1c9352fc06fcebe5c1254ca5e2ad93",
        ["dev/workflow.dev-child-outputs.yaml"] = "sha256:6f9b501303c24b0b52aff07ad626d2307bff3f1d53beb2064fb664cb1bd6b1a6",
        ["dev/workflow.dev-child-summary.yaml"] = "sha256:6f0b1d0202bbc42b982b103b1bc8332af8817446b3d45f012899cc112fbaca9d",
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
