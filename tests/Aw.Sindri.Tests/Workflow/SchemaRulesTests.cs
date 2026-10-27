using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The schema rules (spec §3.2–§3.4, §9.1 "Schema", "Hash"): each rule's invalid fixture gives its exact line.</summary>
public sealed class SchemaRulesTests
{
    private static readonly HashSet<string> _schemaRuleIds = new(["RCP-STATE-001", "RCP-INIT-001", "RCP-DUR-001", "RCP-HASH-001", "RCP-NAME-001"], StringComparer.Ordinal);

    public static TheoryData<string, string> Fixtures => new()
    {
        {
            "RCP-SCHEMA-001",
            "RCP-SCHEMA-001: 'retries' isn't a field of a state at RCP-SCHEMA-001.yaml:10:5 (states.produce.retries); remove it; a state has kind, step, waitFor, timeout, loop, budget, next, outcome, why, displayName, onOutcome, model"
        },
        {
            "RCP-SCHEMA-002",
            "RCP-SCHEMA-002: 'running' isn't active, wait or terminal at RCP-SCHEMA-002.yaml:7:5 (states.produce.kind); write active, wait or terminal"
        },
        {
            "RCP-SCHEMA-003",
            "RCP-SCHEMA-003: anchor '&standard' isn't allowed at RCP-SCHEMA-003.yaml:9:14 (states.produce.timeout); write the value out in full; recipes don't use anchors or aliases"
        },
        {
            "RCP-STATE-001",
            "RCP-STATE-001: state name 'parked' is reserved for the engine at RCP-STATE-001.yaml:11:3 (states.parked); rename it; cancelled, escalated, parked and taken-over are engine-owned states"
        },
        {
            "RCP-INIT-001",
            "RCP-INIT-001: initial state 'done' is a terminal state at RCP-INIT-001.yaml:4:1 (initial); set initial to an active or wait state"
        },
        {
            "RCP-DUR-001",
            "RCP-DUR-001: '90s' isn't a duration ('10m', '4h', '2d', at least 1m) at RCP-DUR-001.yaml:9:16 (states.produce.timeout.after); write a whole number and m, h or d, at least 1m ('10m', '4h', '2d')"
        },
        {
            "RCP-NAME-001",
            "RCP-NAME-001: state name 'produce_it' doesn't match [a-z][a-z0-9-]{0,63} at RCP-NAME-001.yaml:6:3 (states.produce_it); rename it: a lower-case letter, then up to 63 lower-case letters, digits or '-'"
        },
        {
            "RCP-HASH-001",
            "RCP-HASH-001: hash 000000000000 doesn't match the recipe's content, whose hash is 03150aee83c3 at RCP-HASH-001.yaml:16:1 (hash); set hash to sha256:03150aee83c31f5bf016922aabae8ebcc1dbc41a43c36387af0fdbf1d98757c9, or remove it"
        },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Fixtures))]
    public void Each_rule_fixture_reports_its_exact_line(string rule, string line)
    {
        var file = rule + ".yaml";
        var lines = Validate(RecipeFixtures.Read("invalid/" + file), file);

        Assert.Contains(line, lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_missing_or_carrying_a_field_of_another_kind_is_reported()
    {
        var yaml = RecipeReaderTests.Minimal
            .Replace("    next: \"Produce the Step output.\"\n", "    outcome: resolved\n", StringComparison.Ordinal)
            .Replace("    outcome: resolved\ntransitions", "    outcome: resolved\n    timeout: { after: 1h, then: park }\ntransitions", StringComparison.Ordinal);

        var lines = Validate(yaml, "test.yaml");

        Assert.Contains("RCP-SCHEMA-001: active state 'produce' has no 'next' at test.yaml:6:3 (states.produce.next); an active state has step, timeout and next, and may have loop and budget", lines, StringComparer.Ordinal);
        Assert.Contains("RCP-SCHEMA-001: active state 'produce' can't have 'outcome' at test.yaml:10:5 (states.produce.outcome); remove 'outcome'; an active state has step, timeout and next, and may have loop and budget", lines, StringComparer.Ordinal);
        Assert.Contains("RCP-SCHEMA-001: terminal state 'done' can't have 'timeout' at test.yaml:14:5 (states.done.timeout); remove 'timeout'; a terminal state has outcome, and may have next", lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_waitFor_a_when_and_a_with_value_each_name_exactly_one_form()
    {
        var yaml = RecipeReaderTests.Minimal
            .Replace("step: step.produce", "step: { use: step.produce, with: { mode: { fact: fact:item, profile: repos } } }", StringComparison.Ordinal)
            .Replace("when: { outcome: done }", "when: { outcome: done, fact: fact:output }", StringComparison.Ordinal)
            .Replace("  done:\n", "  wait:\n    kind: wait\n    waitFor: { event: item.routed, resource: slot }\n    timeout: { after: 1h, then: park }\n    next: Waiting.\n  done:\n", StringComparison.Ordinal);

        var lines = Validate(yaml, "test.yaml");

        Assert.Contains("RCP-SCHEMA-001: waitFor of state 'wait' names 2 of event, answer, resource and child at test.yaml:13:5 (states.wait.waitFor); name exactly one: { event }, { answer }, { resource } or { child }", lines, StringComparer.Ordinal);
        Assert.Contains("RCP-SCHEMA-001: the condition names 2 forms (outcome, fact) at test.yaml:20:52 (transitions[0].when); use exactly one of { outcome }, { fact, is }, { loop, below: cap }, { all }, { any } or { not }", lines, StringComparer.Ordinal);
        Assert.Contains("RCP-SCHEMA-001: the condition has only one of 'fact' and 'is' at test.yaml:20:52 (transitions[0].when); write { fact: <key>, is: present | absent }", lines, StringComparer.Ordinal);
        Assert.Contains("RCP-SCHEMA-001: 'states.produce.step.with.mode' isn't exactly one of a literal, { fact } or { profile } at test.yaml:8:40 (states.produce.step.with.mode); write a literal (a string, number, bool or a list of these), { fact: <key> } or { profile: <path> }", lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Schema_version_and_a_state_kind_census_are_checked()
    {
        var yaml = RecipeReaderTests.Minimal
            .Replace("schema: 1", "schema: 2", StringComparison.Ordinal)
            .Replace("    kind: active", "    kind: terminal", StringComparison.Ordinal);

        var lines = Validate(yaml, "test.yaml");

        Assert.Contains("RCP-SCHEMA-002: schema is 2, but this build reads schema 1 at test.yaml:1:1 (schema); set schema: 1", lines, StringComparer.Ordinal);
        Assert.Contains("RCP-STATE-001: the recipe has no active or wait state at test.yaml:5:1 (states); add a state with kind: active or kind: wait", lines, StringComparer.Ordinal);
        Assert.Contains("RCP-INIT-001: initial state 'produce' is a terminal state at test.yaml:4:1 (initial); set initial to an active or wait state", lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_from_or_to_an_undeclared_state_is_RCP_SCHEMA_002()
    {
        var yaml = RecipeReaderTests.Minimal
            + "  - { from: [produce, ghost], to: nowhere, on: step.failed }\n";

        var lines = Validate(yaml, "test.yaml");

        Assert.Equal(
            [
                "RCP-SCHEMA-002: transition from 'ghost' names a state the recipe doesn't declare at test.yaml:16:23 (transitions[1].from[1]); name one of the recipe's states: produce, done",
                "RCP-SCHEMA-002: transition to 'nowhere' names a state the recipe doesn't declare at test.yaml:16:31 (transitions[1].to); name one of the recipe's states: produce, done",
            ],
            lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_bad_state_loop_and_parameter_name_is_RCP_NAME_001_once_at_its_place()
    {
        var yaml = RecipeReaderTests.Minimal
            .Replace("initial: produce", "initial: Produce", StringComparison.Ordinal)
            .Replace("  produce:\n", "  Produce:\n", StringComparison.Ordinal)
            .Replace("step: step.produce", "step: { use: step.produce, with: { maxRounds: 3 } }", StringComparison.Ordinal)
            .Replace("from: produce", "from: Produce", StringComparison.Ordinal)
            + "  - { from: Produce, to: done, on: step.failed }\nloops: { Rounds_1: { cap: 2 }, rounds: { cap: 2 } }\n";

        var lines = Validate(yaml, "test.yaml").Where(l => l.StartsWith("RCP-NAME-001", StringComparison.Ordinal));

        Assert.Equal(
            [
                "RCP-NAME-001: state name 'Produce' doesn't match [a-z][a-z0-9-]{0,63} at test.yaml:6:3 (states.Produce); rename it: a lower-case letter, then up to 63 lower-case letters, digits or '-'",
                "RCP-NAME-001: loop name 'Rounds_1' doesn't match [a-z][a-z0-9-]{0,63} at test.yaml:17:10 (loops.Rounds_1); rename it: a lower-case letter, then up to 63 lower-case letters, digits or '-'",
                "RCP-NAME-001: parameter name 'maxRounds' doesn't match [a-z][a-z0-9-]{0,63} at test.yaml:8:40 (states.Produce.step.with.maxRounds); rename it: a lower-case letter, then up to 63 lower-case letters, digits or '-'",
            ],
            lines,
            StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("a")]
    [InlineData("fix-single")]
    [InlineData("taken-over")]
    [InlineData("r2")]
    public void A_lower_case_name_is_no_RCP_NAME_001(string name)
    {
        var yaml = RecipeReaderTests.Minimal.Replace("  done:\n", "  " + name + ":\n    kind: terminal\n    outcome: unresolved\n  done:\n", StringComparison.Ordinal);

        Assert.DoesNotContain(Validate(yaml, "test.yaml"), l => l.StartsWith("RCP-NAME-001", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_terminal_outcome_outside_the_closed_set_is_RCP_OUTCOME_001_alone()
    {
        var yaml = RecipeReaderTests.Minimal.Replace("outcome: resolved", "outcome: cancelled", StringComparison.Ordinal) + "inputs: [{ key: fact:item, type: item, kind: item }]\n";
        var document = RecipeReaderTests.Reader.Read(yaml, "test.yaml").Document!;
        var context = new ValidationContext(
            new BlockCatalog([TestBlocks.Step("step.produce")], []), RecipeProfileFacts.None, InMemoryChildRecipes.None, BaseRecipe: null);

        Assert.Equal(
            ["RCP-OUTCOME-001: terminal state 'done' ends with outcome 'cancelled', which isn't a recipe outcome at test.yaml:13:5 (states.done.outcome); use resolved, unresolved, alreadySatisfied or failed"],
            RecipeValidator.Validate(document, context).Where(f => f.Rule != "RCP-DISPLAY-001").Select(f => f.ToLine()),   // the name is DisplayRulesTests' concern
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_matching_hash_and_a_minimal_recipe_raise_no_schema_finding()
    {
        var hash = RecipeReaderTests.Reader.Read(RecipeReaderTests.Minimal, "test.yaml").Document!.Hash;

        Assert.Empty(Validate(RecipeReaderTests.Minimal + "\nhash: " + hash.Value + "\n", "test.yaml"));
    }

    internal static IReadOnlyList<string> Validate(string yaml, string file)
    {
        var reading = RecipeReaderTests.Reader.Read(yaml, file);
        var context = new ValidationContext(
            new BlockCatalog([TestBlocks.Step("step.produce")], []), RecipeProfileFacts.None, InMemoryChildRecipes.None, BaseRecipe: null);
        var findings = reading.Document is { } document ? [.. reading.Findings, .. RecipeValidator.Validate(document, context)] : reading.Findings;
        return [.. findings.Where(f => f.Rule.StartsWith("RCP-SCHEMA", StringComparison.Ordinal) || _schemaRuleIds.Contains(f.Rule)).Select(f => f.ToLine())];
    }
}
