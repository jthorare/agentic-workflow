using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>RCP-AGENT-001..003 (#1269): a state's agent is approved for Steps, a 'differentFrom' names an earlier agent state, and the two forms are not mixed.</summary>
public sealed class AgentRulesTests
{
    private static Recipe Base(StateAgent? draft, StateAgent? check) =>
        Of(
            "draft",
            [("draft", Active("step.produce") with { Agent = draft }), ("check", Active("step.produce") with { Agent = check }), ("done", Terminal())],
            [Go("draft", "check", when: Outcome("done")), Go("check", "done", when: Outcome("done"))]);

    private static Located Approved(params string[] agents) => Located.Nowhere with { Profile = new RecipeProfileFacts(EffectAllowlist: null) { StepAgents = new HashSet<string>(agents, StringComparer.Ordinal) } };

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_with_no_agent_keys_has_no_findings() => Assert.Empty(CheckAgents(Base(draft: null, check: null), Approved()));

    [Fact, Trait("Lane", "unit")]
    public void An_agent_approved_for_Steps_passes() => Assert.Empty(CheckAgents(Base(new StateAgent(Use: "codex"), check: null), Approved("codex", "claude-code")));

    [Fact, Trait("Lane", "unit")]
    public void An_agent_not_approved_for_Steps_is_RCP_AGENT_001()
    {
        var finding = Assert.Single(CheckAgents(Base(new StateAgent(Use: "codex"), check: null), Approved("claude-code")));

        Assert.Equal(("RCP-AGENT-001", "states.draft.agent"), (finding.Rule, finding.Path));
        Assert.Contains("codex", finding.Message, StringComparison.Ordinal);
        Assert.Contains("stepArgvHash", finding.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_profile_facts_an_agent_is_skipped_not_refused()
    {
        var finding = Assert.Single(CheckAgents(Base(new StateAgent(Use: "codex"), check: null)));

        Assert.Equal(("RCP-AGENT-001", FindingSeverity.Skipped), (finding.Rule, finding.Severity));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_differentFrom_that_names_an_earlier_agent_state_passes() =>
        Assert.Empty(CheckAgents(Base(draft: null, new StateAgent(DifferentFrom: "draft")), Approved()));

    [Fact, Trait("Lane", "unit")]
    public void A_differentFrom_that_names_no_state_is_RCP_AGENT_002()
    {
        var finding = Assert.Single(CheckAgents(Base(draft: null, new StateAgent(DifferentFrom: "nowhere")), Approved()));

        Assert.Equal(("RCP-AGENT-002", "states.check.agent.differentFrom"), (finding.Rule, finding.Path));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_differentFrom_that_names_a_state_with_no_agent_Step_is_RCP_AGENT_002()
    {
        var finding = Assert.Single(CheckAgents(Base(draft: null, new StateAgent(DifferentFrom: "done")), Approved()));

        Assert.Equal("RCP-AGENT-002", finding.Rule);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_that_differs_from_itself_is_RCP_AGENT_003()
    {
        var finding = Assert.Single(CheckAgents(Base(draft: null, new StateAgent(DifferentFrom: "check")), Approved()));

        Assert.Equal(("RCP-AGENT-003", "states.check.agent"), (finding.Rule, finding.Path));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_that_names_an_agent_and_a_state_is_RCP_AGENT_003()
    {
        var finding = Assert.Single(CheckAgents(Base(draft: null, new StateAgent(Use: "codex", DifferentFrom: "draft")), Approved("codex")));

        Assert.Equal("RCP-AGENT-003", finding.Rule);
    }
}
