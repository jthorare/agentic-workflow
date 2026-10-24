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
        ["sindri/workflow.phase.yaml"] = "sha256:95181e1ba5e1724ab20c8de270eb86c3ca49cb4600327c41727efb1836a96096",
        ["sindri/workflow.pr-babysitter.yaml"] = "sha256:e79cbdd5200aaa723398adbd46ec0457c3318d91760163bad1f73f3b765b4f3c",
        ["sindri/workflow.build.yaml"] = "sha256:5f10a81c26f670987bffc06766b8204700daf1bc87e4e13250aca63f4bc931c5",
        ["sindri/workflow.prove.yaml"] = "sha256:65459770b51904fb610708ab41b0be8ba9c4f2943362182397068e129cf14dc5",
        ["sindri/workflow.item.yaml"] = "sha256:fbd360859893f44e3d8c2bd83de7ee8edd760fb66f5541c99f5edb0e89406176",
        ["sindri/workflow.step.yaml"] = "sha256:f039848fd714d2a46a4d3d216083bff1815821cd3e3b4f1bc6402bbb44bd54fa",
        ["sindri/workflow.prove-recheck.yaml"] = "sha256:4884deb4ad0fa009bc2bc0c6bb30bb035cc1591b53f0cae1e1d7c01a840f6eba",
        ["sindri/workflow.docs-sync.yaml"] = "sha256:27292d06aa842d9685c14c153f28587f7ab6dc596e1909cfbd33e3e4722c4822",
        ["sindri/workflow.design.yaml"] = "sha256:e975aac789a6f82a89ed5e4cf6ce80a1127a32a6ff1d0681c136f34e0e7ed1a1",
        ["sindri/workflow.plan.yaml"] = "sha256:60b37848183ead3a2d759893ed3fbbd0577d1fcd5055fa816ef04c2ea326a539",
        ["sindri/workflow.evidence-all.yaml"] = "sha256:5002027b3e1140c2ba5e43440af69ce72874c2d453bb2d282698f3edce629d16",
        ["sindri/workflow.evidence-api.yaml"] = "sha256:65329ff84013c19cafe275bdb82aab95c246be44a5f356ecd6f7383faa40045a",
        ["sindri/workflow.evidence-db.yaml"] = "sha256:467e9d4de75fa907798e9b9df6c127c10b33a596aea2adc049326101cfa3ffeb",
        ["sindri/workflow.evidence-other.yaml"] = "sha256:8349ab5b5fc555f9bceb45dd57bbefbe0687f153b11f6e49312ccc9d6e66b47f",
        ["sindri/workflow.evidence-ui.yaml"] = "sha256:2a869f40551268b9efe06ca191a8c8bd69e4b29e357eccb27caec93b39171958",
        ["sindri/workflow.evidence.yaml"] = "sha256:ee2e0e19f8b0bf43931affe6f5cf565e81aafd5623bb1713c0d99e3ed4d6beed",
        ["sindri/workflow.hand-test.yaml"] = "sha256:d720143f0b0e4951a0171a9b4acabccdcc84ba7d4900771fae469d0a8f5c031c",
        ["sindri/workflow.idea-to-brief.yaml"] = "sha256:f4525721a098360d1241f9324c0aa0c7650199cc0d392e53d4c87853247d9f0d",
        ["sindri/workflow.implement.yaml"] = "sha256:819ca5bc89f796fa768aef156142f0ab725b391524d0e564371ac9f02d751e11",
        ["sindri/workflow.investigate.yaml"] = "sha256:0ffdb4240da9ad6e36574f36a5b50738632e7f302409c3bdb2fc6d4f0274459f",
        ["sindri/workflow.line.yaml"] = "sha256:399889f287367907624340de2e08945029870a0b76f271c30bda56500e55ffe4",
        ["sindri/workflow.report.yaml"] = "sha256:f575d236d52de959680e64936ad543cbc16de0ea5b6be6e63cc9a7403ae1daf1",
        ["sindri/workflow.reproduce.yaml"] = "sha256:e240fa03821f7e4c9ff01fb315ac939f1c18085a9197f4e9ae0b52b732d6f157",
        ["sindri/workflow.review-loop.yaml"] = "sha256:d23cc1f4cbdcff1ac1b3b8f030b6c237d7934bd1820b60168e29a6d6c1fff982",
        ["sindri/workflow.scoping.yaml"] = "sha256:7ecbd470ee0be0afdb0309be2d7314abaf5e83c586481fe5a38a51844f6967d4",
        ["sindri/workflow.ship.yaml"] = "sha256:6f9658d7d5aa4cf2f3dbbd5817028f92c62f95edd6a1bc4dbf0f52c734800660",
        ["sindri/workflow.verify.yaml"] = "sha256:e0ccf98f6b56da60a771eb62255c569293e87191168b3b4f0b2b8eef07611629",
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
