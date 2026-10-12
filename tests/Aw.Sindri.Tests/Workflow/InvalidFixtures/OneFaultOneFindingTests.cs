using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.InvalidFixtures;

/// <summary>
/// Where two rule groups could see the same fault, the specific rule reports it and the generic schema rule doesn't
/// (spec §9.1): each case is the minimal recipe with one fault, read and validated against the shipped catalog.
/// </summary>
public sealed class OneFaultOneFindingTests
{
    public static TheoryData<string, string, string, string> Faults => new()
    {
        { "a missing timeout", "    timeout: { after: 4h, then: park }\n", "", "RCP-TIME-001" },
        { "a terminal state in from", "  - { from: produce,", "  - { from: [produce, done],", "RCP-TRIG-001" },
        { "a terminal outcome outside W6's set", "outcome: resolved", "outcome: cancelled", "RCP-OUTCOME-001" },
        { "a loop cap below 1", "    next: \"Produce", "    loop: rounds\n    next: \"Produce", "RCP-LOOP-001" },
        { "an empty noProgress", "    next: \"Produce", "    loop: rounds\n    next: \"Produce", "RCP-LOOP-003" },
        { "a when testing an undeclared loop", "when: { outcome: done }", "when: { all: [{ outcome: done }, { loop: rounds, below: cap }] }", "RCP-WHEN-002" },
        { "a terminal initial state", "initial: produce", "initial: done", "RCP-INIT-001" },
        { "a Step named as a gate", "produces: [fact:output] }", "produces: [fact:output], gates: [step.verify] }", "RCP-BLOCK-002" },
        { "a wait trigger on an active state", "on: step.completed, when: { outcome: done }", "on: wait.satisfied", "RCP-TRIG-001" },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Faults))]
    public void One_fault_gives_one_finding_from_its_specific_rule(string fault, string find, string replace, string rule)
    {
        var yaml = RecipeReaderTests.Minimal.Replace(find, replace, StringComparison.Ordinal) + Loops(fault);
        var reading = RecipeComposition.Read(yaml, "fault.yaml");
        var document = Assert.IsType<RecipeDocument>(reading.Document);

        var findings = RecipeValidator.Validate(document, new ValidationContext(FixtureCatalog.Shipped(), RecipeProfileFacts.None, InMemoryChildRecipes.None, BaseRecipe: null));

        Assert.Empty(reading.Findings);
        Assert.Equal(rule, Assert.Single(findings, f => f.Severity == FindingSeverity.Error).Rule);
    }

    private static string Loops(string fault) => fault switch
    {
        "a loop cap below 1" => "  - { from: produce, to: produce, on: step.failed }\nloops: { rounds: { cap: 0 } }\n",
        "an empty noProgress" => "  - { from: produce, to: produce, on: step.failed }\nloops: { rounds: { cap: 2, noProgress: [] } }\n",
        _ => string.Empty,
    };
}
