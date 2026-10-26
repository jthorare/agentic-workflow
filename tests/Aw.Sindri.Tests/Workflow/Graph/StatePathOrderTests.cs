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
        ["sindri/workflow.phase.yaml"] = "sha256:7e2491a69f27f7dd772b1523c0b210359436bf386ed0f295648f7c30ae83076c",
        ["sindri/workflow.pr-babysitter.yaml"] = "sha256:84471826e2884e7af7862d154786283d0cca3e2ead272ebef310196561b376a6",
        ["sindri/workflow.build.yaml"] = "sha256:7e9e1d2533ef7a99c70772f7f5678b4aaacab3bfeee517c3f51bdcf65aa4a13e",
        ["sindri/workflow.prove.yaml"] = "sha256:de9bb4356a3ecaf69f9d795bb877a542c0c834e95e53dbeec208cf315791a5b2",
        ["sindri/workflow.item.yaml"] = "sha256:fbd360859893f44e3d8c2bd83de7ee8edd760fb66f5541c99f5edb0e89406176",
        ["sindri/workflow.step.yaml"] = "sha256:4ca37bf46333dc66a14c042bb4561aad96eed677f35f5fc221f0497c8b3e31f7",
        ["sindri/workflow.prove-recheck.yaml"] = "sha256:4884deb4ad0fa009bc2bc0c6bb30bb035cc1591b53f0cae1e1d7c01a840f6eba",
        ["sindri/workflow.docs-sync.yaml"] = "sha256:ce9881860d56fa4a1948b7e285208901edcd73c135da018a156c124b77d2e0ac",
        ["sindri/workflow.design.yaml"] = "sha256:cbed489995a8e581c774450b613dce16542ea5fe7f950ee97be8650485dc413b",
        ["sindri/workflow.plan.yaml"] = "sha256:26db0ec430e5d53500f61e3c28a6e2160fe9378461cfd27266225a2c5ef9e88b",
        ["sindri/workflow.evidence-all.yaml"] = "sha256:5002027b3e1140c2ba5e43440af69ce72874c2d453bb2d282698f3edce629d16",
        ["sindri/workflow.evidence-api.yaml"] = "sha256:65329ff84013c19cafe275bdb82aab95c246be44a5f356ecd6f7383faa40045a",
        ["sindri/workflow.evidence-db.yaml"] = "sha256:467e9d4de75fa907798e9b9df6c127c10b33a596aea2adc049326101cfa3ffeb",
        ["sindri/workflow.evidence-other.yaml"] = "sha256:8349ab5b5fc555f9bceb45dd57bbefbe0687f153b11f6e49312ccc9d6e66b47f",
        ["sindri/workflow.evidence-ui.yaml"] = "sha256:df264392e850ef9c2181b30b705dfbd250655e747555b23e15b62b5370dd2642",
        ["sindri/workflow.evidence.yaml"] = "sha256:09e6ad3775beb9c08ed210e45d930b9cd53b4a32fd20c9f7f71cb2d1961dac02",
        ["sindri/workflow.hand-test.yaml"] = "sha256:d720143f0b0e4951a0171a9b4acabccdcc84ba7d4900771fae469d0a8f5c031c",
        ["sindri/workflow.idea-to-brief.yaml"] = "sha256:dce79edf3b6929ac787ba5fc2a933438901ae4d63f7cc39b66e25b7eb27afcb5",
        ["sindri/workflow.implement.yaml"] = "sha256:40e987976b6cc3ad1b93529b6914166f3bcea3e3d7aa2c062f36ef227e3c1b2c",
        ["sindri/workflow.investigate.yaml"] = "sha256:02bc6614a2e4cde094c4f8e20087233fd9448c3ca5397731c2a326bd4a533099",
        ["sindri/workflow.line.yaml"] = "sha256:3af2aaac7b5dac9a9720cd80c00f0a010c8f31f989d29793504d8e2970c5213e",
        ["sindri/workflow.report.yaml"] = "sha256:f575d236d52de959680e64936ad543cbc16de0ea5b6be6e63cc9a7403ae1daf1",
        ["sindri/workflow.reproduce.yaml"] = "sha256:4902709f99aaa03fbb86a2d1f8f3b47737bbd4663588191d7cb42d8c1314739f",
        ["sindri/workflow.review-loop.yaml"] = "sha256:55e1efd18ae65ce28f72557b5f88389f5f193c36aac706865a74267be4572995",
        ["sindri/workflow.scoping.yaml"] = "sha256:84ed8d02a3d8f20b6aa910a904e9b036e98bf766e54202e21e978abef89d0b1f",
        ["sindri/workflow.ship.yaml"] = "sha256:6f9658d7d5aa4cf2f3dbbd5817028f92c62f95edd6a1bc4dbf0f52c734800660",
        ["sindri/workflow.verify.yaml"] = "sha256:e0ccf98f6b56da60a771eb62255c569293e87191168b3b4f0b2b8eef07611629",
        ["sindri/workflow.evidence-desktop.yaml"] = "sha256:4c4b01ad403c7856dfe2c58a9e1f1e03c2a6d19e03a07d291371f7f777cb46f1",
        ["dev/desk-round-trip.yaml"] = "sha256:22e3d173c3030036026aa70ae192f0bbe64b5135960ec38d7f05976443595357",
        ["dev/incident-hold.yaml"] = "sha256:fae531ac58b3c3990c51a125548aa83e832f4d0dd77231d272e860b067b7cda1",
        ["dev/triage-three-state.yaml"] = "sha256:b77152c8613ea86d8a64fdf9e9a140c23b1c9352fc06fcebe5c1254ca5e2ad93",
        ["dev/workflow.dev-agent-hold.yaml"] = "sha256:41c85c4666f946ca5489eb53dfc9c754508866044a28a5ba39e39c884b35dc19",
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
