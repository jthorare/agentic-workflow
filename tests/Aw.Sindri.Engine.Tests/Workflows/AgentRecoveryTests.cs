using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The decision for an agent run whose session ended with no report (issue #1000): complete only when the outputs prove exactly one outcome, else park naming the folder.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentRecoveryTests
{
    private const string Folder = "/work/app/.sindri-out";
    private static readonly FactKey _round = FactKey.From("fact:challengeRound");
    private static readonly FactKey _other = FactKey.From("fact:other");
    private static readonly OutcomeName _found = OutcomeName.From("found");
    private static readonly OutcomeName _none = OutcomeName.From("none");

    /// <summary>The scope challenge's shape: 'found' needs the round, 'none' needs nothing.</summary>
    private static readonly BlockDescriptor _challenge = TestBlocks.Step("step.scope.challenge") with
    {
        Outputs = [new OutputSpec(_round, [_found])],
        Outcomes = [_found, _none],
    };

    [Fact]
    public void Outputs_that_prove_one_outcome_complete_the_run_with_that_outcome_and_each_fact_s_file()
    {
        var decision = AgentRecovery.Decide(_challenge, ["challengeRound"], Folder);

        Assert.Equal(_found, decision.Outcome);
        Assert.Equal("challengeRound", Assert.Single(decision.Files).Value);
        Assert.Null(decision.Reason);
    }

    [Fact]
    public void A_Step_with_one_outcome_completes_from_its_outputs()
    {
        var one = TestBlocks.Step("step.produce");

        var decision = AgentRecovery.Decide(one, ["output"], Folder);

        Assert.Equal(OutcomeName.From("done"), decision.Outcome);
    }

    [Fact]
    public void Files_that_are_not_the_Step_s_outputs_park_the_run_naming_the_folder()
    {
        var decision = AgentRecovery.Decide(_challenge, ["notes.md"], Folder);

        Assert.Null(decision.Outcome);
        Assert.StartsWith("the agent finished but its result was not recorded: " + Folder, decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Some_of_the_outputs_an_outcome_needs_park_the_run()
    {
        var two = _challenge with { Outputs = [new OutputSpec(_round, [_found]), new OutputSpec(_other, [_found])] };

        var decision = AgentRecovery.Decide(two, ["challengeRound"], Folder);

        Assert.Null(decision.Outcome);
        Assert.Contains(Folder, decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Outputs_that_fit_two_outcomes_park_the_run_because_the_choice_is_not_guessed()
    {
        var both = _challenge with { Outputs = [new OutputSpec(_round, [_found, _none])] };

        var decision = AgentRecovery.Decide(both, ["challengeRound"], Folder);

        Assert.Null(decision.Outcome);
    }

    [Fact]
    public void A_Step_that_needs_no_output_is_never_completed_from_a_folder()
    {
        var bare = _challenge with { Outputs = [], Outcomes = [_none] };

        Assert.Null(AgentRecovery.Decide(bare, ["anything"], Folder).Outcome);
    }

    [Fact]
    public void A_Step_that_picks_its_outcome_from_its_parameters_parks()
    {
        var chooser = _challenge with { OutcomesFrom = "choices" };

        Assert.Null(AgentRecovery.Decide(chooser, ["challengeRound"], Folder).Outcome);
    }

    [Fact]
    public void A_Step_the_catalog_does_not_know_parks() => Assert.Null(AgentRecovery.Decide(descriptor: null, ["challengeRound"], Folder).Outcome);
}
