using static Aw.Sindri.Tests.Workflow.Graph.GraphRecipe;

namespace Aw.Sindri.Tests.Workflow.Graph;

/// <summary>RCP-TRIG-001 (the trigger fits its source state's kind) and RCP-TRIG-002 (no reserved verb), spec §3.5 and §6.3.</summary>
public sealed class TriggerRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_wait_trigger_from_an_active_state_is_refused()
    {
        // The recipe of invalid/RCP-TRIG-001.yaml; the 'on' of transitions[0] is at 15:32.
        var recipe = Of("produce", [("produce", Active()), ("done", Terminal())], [Go("produce", "done", "wait.satisfied")], id: "workflow.trigger");

        Assert.Equal(
            ["RCP-TRIG-001: trigger 'wait.satisfied' doesn't fit active state 'produce' at RCP-TRIG-001.yaml:15:32 (transitions[0].on); from an active state, use step.completed, step.failed or command.<verb>"],
            Lines(recipe, "RCP-TRIG-001.yaml", [("transitions[0].on", 15, 32)]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("step.completed")]
    [InlineData("step.failed")]
    public void A_step_trigger_from_a_wait_state_is_refused(string on)
    {
        var recipe = Of("triaged", [("triaged", WaitForEvent("item.routed")), ("done", Terminal())], [Go("triaged", "done", on)]);

        Assert.Equal(
            [$"RCP-TRIG-001: trigger '{on}' doesn't fit wait state 'triaged' at r.yaml:1:1 (transitions[0].on); from a wait state, use wait.satisfied or command.<verb>"],
            Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_from_a_terminal_state_is_refused()
    {
        var recipe = Of("produce", [("produce", Active()), ("done", Terminal())], [Go("produce", "done"), Go("done", "produce", "command.reopen")]);

        Assert.Equal(
            ["RCP-TRIG-001: trigger 'command.reopen' doesn't fit terminal state 'done' at r.yaml:1:1 (transitions[1].on); leave terminal states out of from, since nothing leaves one"],
            Lines(recipe));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("step.done")]
    [InlineData("completed")]
    [InlineData("commands.retry")]
    public void An_unknown_trigger_is_refused(string on)
    {
        var recipe = Of("produce", [("produce", Active()), ("done", Terminal())], [Go("produce", "done"), Go("produce", "done", on)]);

        Assert.Equal(
            [$"RCP-TRIG-001: trigger '{on}' isn't step.completed, step.failed, wait.satisfied or command.<verb> at r.yaml:1:1 (transitions[1].on); use one of those triggers"],
            Lines(recipe));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("command.")]
    [InlineData("command.x")]
    [InlineData("command.Retry")]
    [InlineData("command.re_try")]
    [InlineData("command.-retry")]
    [InlineData("command.abcdefghijklmnopqrstuvwxyzabcdefg")]
    public void A_verb_outside_the_grammar_is_refused(string on)
    {
        var recipe = Of("produce", [("produce", Active()), ("done", Terminal())], [Go("produce", "done"), Go("produce", "done", on)]);

        Assert.Equal(
            [$"RCP-TRIG-001: trigger '{on}' has a verb that doesn't match [a-z][a-z-]{{1,31}} at r.yaml:1:1 (transitions[1].on); name the verb with 2 to 32 lowercase letters and dashes, starting with a letter"],
            Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reserved_verb_is_refused()
    {
        // The recipe of invalid/RCP-TRIG-002.yaml; the 'on' of transitions[1] is at 16:32.
        var recipe = Of(
            "produce",
            [("produce", Active()), ("done", Terminal())],
            [Go("produce", "done"), Go("produce", "done", "command.cancel")],
            id: "workflow.trigger-reserved");

        Assert.Equal(
            ["RCP-TRIG-002: verb 'cancel' is reserved for sindri workflow's own commands at RCP-TRIG-002.yaml:16:32 (transitions[1].on); choose another verb"],
            Lines(recipe, "RCP-TRIG-002.yaml", [("transitions[1].on", 16, 32)]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("answer")]
    [InlineData("grant-tokens")]
    [InlineData("command")]
    [InlineData("freeze")]
    public void Every_verb_on_the_reserved_list_is_refused(string verb)
    {
        var recipe = Of("produce", [("produce", Active()), ("done", Terminal())], [Go("produce", "done"), Go("produce", "done", "command." + verb)]);

        _ = Assert.Single(Lines(recipe), line => line.StartsWith($"RCP-TRIG-002: verb '{verb}' is reserved", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Triggers_that_fit_their_source_raise_nothing()
    {
        var recipe = Of(
            "produce",
            [("produce", Active()), ("claimed", WaitForEvent("claim.created")), ("done", Terminal()), ("dropped", Terminal("unresolved"))],
            [
                Go("produce", "claimed"),
                Go("produce", "dropped", "step.failed"),
                Go("claimed", "done", "wait.satisfied"),
                Go("claimed", "dropped", "command.give-up") with { From = ["produce", "claimed"] },
            ]);

        Assert.Empty(Lines(recipe));
    }
}
