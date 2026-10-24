using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>The scoping Steps that reach the context engine on the block runtime (scoping spec §5.1, C1, C9): anchor, reanchor, propose and withdraw.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopingIoStepTests
{
    private static readonly RepoName _repo = RepoName.From("fixture-notes");
    private static readonly InstanceAddress _address = InstanceAddress.From("T-1/scoping/1");

    private static StepContext Context(string step, ScopingFacts facts, RepoName? repo = null) =>
        ScopingStepContexts.Of(step, facts) with { Repo = repo ?? _repo, Address = _address.Value };

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private static string[] Strings(JsonNode node) => [.. node.AsArray().Select(static x => x!.GetValue<string>())];

    [Fact]
    public async Task Anchor_asks_the_text_shape_over_the_briefs_text_and_records_what_it_suggests()
    {
        var shape = new InMemoryScopingTextShape(["feature:links", "surface:dialog/share"]);

        var result = await new ScopeAnchorStep(shape).Run(Context("step.scope.anchor", new ScopingFacts { ["brief"] = ScopingFacts.Expiry()["brief"] }), TestContext.Current.CancellationToken);

        Assert.Equal("anchored", result.Result!.Outcome.Value);
        Assert.Equal(["feature:links", "surface:dialog/share"], Strings(Out(result, "anchors")), StringComparer.Ordinal);
        var (repo, text) = Assert.Single(shape.Asked);
        Assert.Equal(_repo, repo);
        Assert.Equal(JsonNode.Parse(ScopingFacts.Expiry()["brief"])!["text"]!.GetValue<string>(), text);
    }

    [Fact]
    public async Task Anchor_fails_the_Step_with_the_engines_error_when_it_cannot_answer()
    {
        var shape = new InMemoryScopingTextShape(SindriErrors.InvalidInput.With("the question needs at least one word"));

        var result = await new ScopeAnchorStep(shape).Run(Context("step.scope.anchor", new ScopingFacts { ["brief"] = ScopingFacts.Expiry()["brief"] }), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("at least one word", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reanchor_adds_the_text_shapes_suggestions_to_the_maps_own_keys()
    {
        var shape = new InMemoryScopingTextShape(["feature:sharing"]);

        var result = await new ScopeReanchorStep(shape).Run(Context("step.scope.reanchor", Reanchorable()), TestContext.Current.CancellationToken);

        var anchors = Strings(Out(result, "anchors"));
        Assert.Contains("feature:sharing", anchors, StringComparer.Ordinal);
        Assert.Contains("surface:route/links-create", anchors, StringComparer.Ordinal);
        Assert.Contains("code:migrations/006_link_expiry.sql", anchors, StringComparer.Ordinal);
        Assert.Contains("Public link sharing", Assert.Single(shape.Asked).Text, StringComparison.Ordinal);   // the surfaces' titles are the text asked about
    }

    [Fact]
    public async Task Reanchor_without_an_answer_from_the_engine_still_anchors_from_the_map()
    {
        var shape = new InMemoryScopingTextShape(SindriErrors.InvalidInput.With("no map"));

        var result = await new ScopeReanchorStep(shape).Run(Context("step.scope.reanchor", Reanchorable()), TestContext.Current.CancellationToken);

        Assert.Equal("anchored", result.Result!.Outcome.Value);
        Assert.Contains("surface:route/links-create", Strings(Out(result, "anchors")), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Propose_submits_the_surfaces_with_no_node_and_records_the_statuses_the_engine_answers()
    {
        var proposals = new InMemoryScopeRunProposals();

        var result = await new ScopeProposeStep(proposals).Run(Context("step.scope.propose", new ScopingFacts { ["scopeMap"] = Unproposed() }), TestContext.Current.CancellationToken);

        Assert.Equal("proposed", result.Result!.Outcome.Value);
        var (run, repo, surfaces) = Assert.Single(proposals.Calls);
        Assert.Equal((_address, _repo), (run, repo));
        Assert.Equal(2, surfaces.Count);
        var mapped = Out(result, "scopeMap")["surfaces"]!.AsArray();
        Assert.Equal("new", mapped.Single(static s => s!["id"]!.GetValue<string>() == "S22")!["graphStatus"]!.GetValue<string>());
        Assert.Equal("surface:proposed/expiry-choice-in-the-share-dialog", mapped.Single(static s => s!["id"]!.GetValue<string>() == "S22")!["key"]!.GetValue<string>());
        Assert.Equal("mapped", mapped.Single(static s => s!["id"]!.GetValue<string>() == "S2")!["graphStatus"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_revise_pass_sends_the_first_passes_proposals_again_so_they_stay()
    {
        var proposals = new InMemoryScopeRunProposals();
        var first = await new ScopeProposeStep(proposals).Run(Context("step.scope.propose", new ScopingFacts { ["scopeMap"] = Unproposed() }), TestContext.Current.CancellationToken);

        var second = await new ScopeProposeStep(proposals).Run(Context("step.scope.propose", new ScopingFacts { ["scopeMap"] = Out(first, "scopeMap").ToJsonString() }), TestContext.Current.CancellationToken);

        Assert.Equal("proposed", second.Result!.Outcome.Value);
        Assert.Equal(2, proposals.Calls.Count);
        Assert.Equal(proposals.Calls[0].Surfaces.Select(static s => s.Title), proposals.Calls[1].Surfaces.Select(static s => s.Title), StringComparer.Ordinal);
        Assert.Equal(2, proposals.Held(_address).Count);   // the second pass replaced the run's proposals with the same two, it did not withdraw them
    }

    [Fact]
    public async Task A_map_with_nothing_to_propose_sends_nothing_rather_than_withdrawing_the_runs_proposals()
    {
        var proposals = new InMemoryScopeRunProposals();

        var result = await new ScopeProposeStep(proposals).Run(Context("step.scope.propose", new ScopingFacts { ["scopeMap"] = AllMapped() }), TestContext.Current.CancellationToken);

        Assert.Empty(proposals.Calls);
        Assert.Equal("skipped", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Propose_with_the_engine_down_reports_skipped_and_leaves_the_statuses_unknown()
    {
        var proposals = new InMemoryScopeRunProposals(SindriErrors.InvalidInput.With("down"));

        var result = await new ScopeProposeStep(proposals).Run(Context("step.scope.propose", new ScopingFacts { ["scopeMap"] = Unproposed() }), TestContext.Current.CancellationToken);

        Assert.Equal("skipped", result.Result!.Outcome.Value);
        Assert.Equal("unknown", Out(result, "scopeMap")["surfaces"]!.AsArray().Single(static s => s!["id"]!.GetValue<string>() == "S22")!["graphStatus"]!.GetValue<string>());
    }

    [Fact]
    public async Task Propose_for_an_item_with_no_repo_submits_nothing_and_reports_skipped()
    {
        var proposals = new InMemoryScopeRunProposals();
        var context = ScopingStepContexts.Of("step.scope.propose", new ScopingFacts { ["scopeMap"] = Unproposed() });

        var result = await new ScopeProposeStep(proposals).Run(context, TestContext.Current.CancellationToken);

        Assert.Equal("skipped", result.Result!.Outcome.Value);
        Assert.Empty(proposals.Calls);
    }

    [Fact]
    public async Task Withdraw_resubmits_the_run_with_no_surfaces()
    {
        var proposals = new InMemoryScopeRunProposals();

        var result = await new ScopeWithdrawStep(proposals).Run(Context("step.scope.withdraw", []), TestContext.Current.CancellationToken);

        Assert.Equal("withdrawn", result.Result!.Outcome.Value);
        Assert.Empty(result.Result.Outputs);
        var (run, repo, surfaces) = Assert.Single(proposals.Calls);
        Assert.Equal((_address, _repo), (run, repo));
        Assert.Empty(surfaces);
    }

    [Fact]
    public async Task Withdraw_fails_when_the_engine_cannot_take_the_proposals_back()
    {
        var result = await new ScopeWithdrawStep(new InMemoryScopeRunProposals(SindriErrors.InvalidInput.With("down"))).Run(Context("step.scope.withdraw", []), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("not withdrawn", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Withdraw_for_an_item_with_no_repo_has_nothing_to_take_back()
    {
        var proposals = new InMemoryScopeRunProposals();

        var result = await new ScopeWithdrawStep(proposals).Run(ScopingStepContexts.Of("step.scope.withdraw", []), TestContext.Current.CancellationToken);

        Assert.Equal("withdrawn", result.Result!.Outcome.Value);
        Assert.Empty(proposals.Calls);
    }

    private static ScopingFacts Reanchorable() => new()
    {
        ["scopeMap"] = ScopingFacts.Expiry()["scopeMap"],
        ["anchors"] = ScopingFacts.Expiry()["anchors"],
        ["lenses"] = ScopingFacts.Expiry()["lenses"],
    };

    /// <summary>The gold map with every surface already on the map: nothing is left to propose.</summary>
    private static string AllMapped() => ScopingFacts.Expiry().Edit("scopeMap", map =>
    {
        foreach (var surface in map["surfaces"]!.AsArray().OfType<JsonObject>())
        {
            surface["graphStatus"] = "mapped";
        }
    })["scopeMap"];

    /// <summary>The gold map as it stood before propose: S22 and S23 have no node key yet.</summary>
    private static string Unproposed() => ScopingFacts.Expiry().Edit("scopeMap", map =>
    {
        foreach (var surface in map["surfaces"]!.AsArray().OfType<JsonObject>().Where(static s => string.Equals(s["id"]!.GetValue<string>(), "S22", StringComparison.Ordinal) || string.Equals(s["id"]!.GetValue<string>(), "S23", StringComparison.Ordinal)))
        {
            _ = surface.Remove("key");
            surface["graphStatus"] = "unknown";
        }
    })["scopeMap"];
}
