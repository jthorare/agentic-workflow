using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.PlanContext;
using Aw.Sindri.Testing.Line;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.PlanContext;

/// <summary>LN-CTX1: after the owner's approve, the design's surfaces go to the map as the plan instance's proposals and stop 2's answers as owner decision records; nothing is sent from an unapproved draft.</summary>
public sealed class ProposePlanContextTests
{
    private static readonly InstanceAddress _run = InstanceAddress.From("ITM-1/plan/1");
    private static readonly RepoName _repo = RepoName.From("notes");
    private const string Packet = "sha256:3deb049307bf38e0981ed4ed374f7d84a63f305f0067a247c10f839dfd4f87e4";

    private static (ProposePlanContextService Service, InMemoryScopeRunProposals Proposals, InMemoryOwnerDecisions Decisions) Subject(Error? proposalError = null, Error? decisionError = null)
    {
        var proposals = proposalError is { } p ? new InMemoryScopeRunProposals(p) : new InMemoryScopeRunProposals();
        var decisions = decisionError is { } d ? new InMemoryOwnerDecisions(d) : new InMemoryOwnerDecisions();
        return (new ProposePlanContextService(proposals, decisions), proposals, decisions);
    }

    private static Task<Result<ProposePlanContextResponse>> Run(ProposePlanContextService service, LineFixture facts, bool approved = true) =>
        service.Handle(new ProposePlanContextRequest(facts, _run, _repo, Packet, approved), CancellationToken.None).AsTask();

    private static LineFixture Gold()
    {
        var facts = LineFixture.Plan();
        facts["design/coverage.json"] = facts["design/coverage.json"];
        return facts;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_approved_design_proposes_the_surfaces_the_map_lacks_with_the_plan_instance_as_the_run()
    {
        var (service, proposals, _) = Subject();

        var result = await Run(service, Gold());

        Assert.Equal("proposed", result.Value.Outcome);
        var call = Assert.Single(proposals.Calls);
        Assert.Equal((_run, _repo), (call.Run, call.Repo));
        Assert.Equal([("route/export-download", "surface"), ("migrations/006_link_expiry.sql", null)], call.Surfaces.Select(s => (s.Title, s.Kind)));
        Assert.Equal(2, result.Value.Surfaces.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unapproved_draft_sends_nothing_at_all()
    {
        var (service, proposals, decisions) = Subject();

        var result = await Run(service, Gold(), approved: false);

        Assert.Equal("skipped", result.Value.Outcome);
        Assert.Equal("the owner has not approved the design", result.Value.Reason);
        Assert.Empty(proposals.Calls);
        Assert.Empty(decisions.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_answers_become_owner_decision_records_linked_to_the_surfaces_they_cite_keyed_by_the_packet()
    {
        var (service, _, decisions) = Subject();

        var result = await Run(service, Gold());

        var call = Assert.Single(decisions.Calls);
        Assert.Equal((_run, Packet), (call.Run, call.Packet));
        Assert.Equal(3, call.Decisions.Count);
        Assert.Equal(3, result.Value.Decisions);
        var d3 = call.Decisions[2];
        Assert.Equal("Where does link expiry run: on read, or in the nightly job? Checked on read, in the public route.", d3.Text);
        Assert.Equal(["surface:route/link-resolve"], d3.Nodes, StringComparer.Ordinal);
        Assert.Equal("A1", d3.Answer);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_the_latest_pick_of_a_decision_is_stored_with_the_entry_it_came_from()
    {
        var facts = Gold().EditText("answers.jsonl", t => "{\"batch\":1,\"actor\":\"owner\",\"stop\":\"after-design\",\"answers\":[{\"decision\":\"D3\",\"pick\":\"D3.b\"}]}\n" +
            "{\"batch\":2,\"actor\":\"owner\",\"stop\":\"after-design\",\"answers\":[{\"decision\":\"D1\",\"pick\":\"D1.a\"},{\"decision\":\"D2\",\"pick\":\"D2.a\"},{\"decision\":\"D3\",\"pick\":\"D3.a\"}]}\n");
        var (service, _, decisions) = Subject();

        _ = await Run(service, facts);

        var stored = Assert.Single(decisions.Calls).Decisions;
        Assert.Equal(3, stored.Count);
        Assert.All(stored, d => Assert.Equal("A2", d.Answer));
        Assert.DoesNotContain(stored, d => d.Text.Contains("nightly job. Swept", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_the_owner_has_not_answered_is_not_stored()
    {
        var facts = Gold().EditText("answers.jsonl", _ => "{\"batch\":1,\"actor\":\"owner\",\"stop\":\"after-design\",\"answers\":[{\"decision\":\"D1\",\"pick\":\"D1.a\"}]}\n");
        var (service, _, decisions) = Subject();

        var result = await Run(service, facts);

        Assert.Equal(1, result.Value.Decisions);
        _ = Assert.Single(Assert.Single(decisions.Calls).Decisions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pick_that_names_no_option_is_not_stored()
    {
        var facts = Gold().EditText("answers.jsonl", _ => "{\"batch\":1,\"actor\":\"owner\",\"stop\":\"after-design\",\"answers\":[{\"decision\":\"D1\",\"pick\":\"D1.z\"},{\"decision\":\"D9\",\"pick\":\"D9.a\"}]}\n");
        var (service, _, decisions) = Subject();

        var result = await Run(service, facts);

        Assert.Equal(0, result.Value.Decisions);
        Assert.Empty(decisions.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_design_with_no_new_surface_sends_no_proposal_since_an_empty_list_would_withdraw_the_runs_earlier_ones()
    {
        var facts = Gold().Edit("design/escapes.json", n => n["escapes"] = new JsonArray()).Edit("design/coverage.json", n => n["unmapped"] = new JsonArray());
        var (service, proposals, _) = Subject();

        var result = await Run(service, facts);

        Assert.Empty(proposals.Calls);
        Assert.Empty(result.Value.Surfaces);
        Assert.Equal("proposed", result.Value.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_context_engine_that_cannot_answer_skips_the_proposals_and_the_decisions_still_go()
    {
        var (service, _, decisions) = Subject(proposalError: SindriErrors.InvalidInput.With("down"));

        var result = await Run(service, Gold());

        Assert.Equal("proposed", result.Value.Outcome);
        Assert.Contains("the context engine could not take the surfaces", result.Value.Reason, StringComparison.Ordinal);
        Assert.Empty(result.Value.Surfaces);
        _ = Assert.Single(decisions.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_ingest_that_does_not_exist_yet_is_reported_and_is_no_failure()
    {
        var service = new ProposePlanContextService(new InMemoryScopeRunProposals(), new Infrastructure.Packets.PendingOwnerDecisions());

        var result = await Run(service, Gold());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Decisions);
        Assert.Contains("C14", result.Value.Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_ingest_that_fails_fails_the_step()
    {
        var (service, _, _) = Subject(decisionError: SindriErrors.InvalidInput.With("ingest failed"));

        var result = await Run(service, Gold());

        Assert.True(result.IsError);
    }
}
