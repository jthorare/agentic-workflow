using System.Text.Json;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The YAML recipe reader (spec §3.1): core-schema scalars, every node's position, every finding, and normalization of each short form.</summary>
public sealed class RecipeReaderTests
{
    internal const string Minimal =
        "schema: 1\n"
        + "id: workflow.minimal\n"
        + "version: 1\n"
        + "initial: produce\n"
        + "states:\n"
        + "  produce:\n"
        + "    kind: active\n"
        + "    step: step.produce\n"
        + "    timeout: { after: 4h, then: park }\n"
        + "    next: \"Produce the Step output.\"\n"
        + "  done:\n"
        + "    kind: terminal\n"
        + "    outcome: resolved\n"
        + "transitions:\n"
        + "  - { from: produce, to: done, on: step.completed, when: { outcome: done }, produces: [fact:output] }\n";

    internal static IRecipeReader Reader { get; } = new YamlRecipeReader(SliceHarness.Json);

    internal static Recipe Bind(string yaml)
    {
        var reading = Reader.Read(yaml, "test.yaml");
        Assert.Empty(reading.Findings.Select(f => f.ToLine()));
        return reading.Document!.Recipe;
    }

    internal static IReadOnlyList<string> Lines(string yaml) => [.. Reader.Read(yaml, "test.yaml").Findings.Select(f => f.ToLine())];

    [Fact, Trait("Lane", "unit")]
    public void A_minimal_recipe_binds_with_no_findings()
    {
        var reading = Reader.Read(Minimal, "test.yaml");

        Assert.Empty(reading.Findings);
        Assert.Equal("workflow.minimal", reading.Document!.Recipe.Id.Value);
        Assert.Equal("test.yaml", reading.Document.Source.File);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_hyphenated_fact_key_is_RCP_SCHEMA_002_with_a_camelCase_fix()
    {
        var lines = Lines(Minimal.Replace("produces: [fact:output]", "produces: [fact:evidence-summary]", StringComparison.Ordinal));

        var line = Assert.Single(lines);
        Assert.StartsWith("RCP-SCHEMA-002: 'fact:evidence-summary' isn't a fact key (", line, StringComparison.Ordinal);
        Assert.EndsWith("; fact names are camelCase: write fact:evidenceSummary", line, StringComparison.Ordinal);
    }

    private static string WithProduces(string produces) =>
        Minimal.Replace("produces: [fact:output]", "produces: " + produces, StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_long_form_produces_entry_binds_with_its_source()
    {
        var recipe = Bind(WithProduces("[fact:output, { fact: fact:exported, from: fact:result }]"));

        Assert.Equal(
            [new ProducedFact(FactKey.From("fact:output"), From: null), new ProducedFact(FactKey.From("fact:exported"), FactKey.From("fact:result"))],
            recipe.Transitions[0].Produces);
        Assert.Equal(FactKey.From("fact:result"), recipe.Transitions[0].Produces[1].SourceKey());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rename_to_itself_reads_as_the_bare_key_and_hashes_the_same()
    {
        var renamed = Reader.Read(WithProduces("[{ fact: fact:output, from: fact:output }]"), "a.yaml").Document!;
        var bare = Reader.Read(Minimal, "a.yaml").Document!;

        Assert.Equal([new ProducedFact(FactKey.From("fact:output"), From: null)], renamed.Recipe.Transitions[0].Produces);
        Assert.Equal(bare.Hash, renamed.Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_produced_fact_whose_source_is_itself_is_the_bare_key_in_the_type_and_the_converter()
    {
        var x = FactKey.From("fact:output");
        var built = new ProducedFact(x, x);

        Assert.Equal(new ProducedFact(x, From: null), built);
        Assert.Null(built.From);
        Assert.Equal("\"fact:output\"", JsonSerializer.Serialize(built));
        Assert.Equal(new ProducedFact(x, From: null), JsonSerializer.Deserialize<ProducedFact>("{\"fact\":\"fact:output\",\"from\":\"fact:output\"}"));
        Assert.Equal(new ProducedFact(x, FactKey.From("fact:result")), JsonSerializer.Deserialize<ProducedFact>("{\"fact\":\"fact:output\",\"from\":\"fact:result\"}"));

        // with-expressions go through the same normalization, in both directions
        var renamed = new ProducedFact(x, FactKey.From("fact:result"));
        var toSelf = renamed with { From = x };
        var toOther = new ProducedFact(x, From: null) with { From = FactKey.From("fact:result") };
        Assert.Equal(new ProducedFact(x, From: null), toSelf);
        Assert.Null(toSelf.From);
        Assert.Equal("\"fact:output\"", JsonSerializer.Serialize(toSelf));
        Assert.Equal(renamed, toOther);
        Assert.Equal("{\"fact\":\"fact:output\",\"from\":\"fact:result\"}", JsonSerializer.Serialize(toOther));

        // a with that changes the fact leaves a source equal to the new fact normalized too
        var y = FactKey.From("fact:result");
        var moved = new ProducedFact(x, x) with { Fact = y };
        Assert.Equal(new ProducedFact(y, From: null), moved);
        Assert.Equal("\"fact:result\"", JsonSerializer.Serialize(moved));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_long_form_entry_without_from_is_RCP_SCHEMA_001()
    {
        var line = Assert.Single(Lines(WithProduces("[{ fact: fact:exported }]")));

        Assert.StartsWith("RCP-SCHEMA-001: a produces entry has no 'from'", line, StringComparison.Ordinal);
        Assert.Contains("transitions[0].produces[0].from", line, StringComparison.Ordinal);
        Assert.EndsWith("add 'from': the child's output key, or write the bare key", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_field_in_a_produces_entry_is_RCP_SCHEMA_001()
    {
        var line = Assert.Single(Lines(WithProduces("[{ fact: fact:exported, from: fact:result, as: fact:x }]")));

        Assert.StartsWith("RCP-SCHEMA-001: 'as' isn't a field of a produces entry", line, StringComparison.Ordinal);
        Assert.EndsWith("remove it; a produces entry has fact, from", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_hyphenated_from_is_RCP_SCHEMA_002_with_a_camelCase_fix()
    {
        var line = Assert.Single(Lines(WithProduces("[{ fact: fact:exported, from: fact:my-result }]")));

        Assert.StartsWith("RCP-SCHEMA-002: 'fact:my-result' isn't a fact key (", line, StringComparison.Ordinal);
        Assert.Contains("transitions[0].produces[0].from", line, StringComparison.Ordinal);
        Assert.EndsWith("; fact names are camelCase: write fact:myResult", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_list_in_produces_is_RCP_SCHEMA_002()
    {
        var line = Assert.Single(Lines(WithProduces("[[fact:output]]")));

        Assert.StartsWith("RCP-SCHEMA-002: [\"fact:output\"] isn't a fact key, or { fact, from }", line, StringComparison.Ordinal);
        Assert.EndsWith("write a fact key, or { fact, from } to rename a child's output", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void On_yes_and_no_stay_text_under_the_core_schema_while_true_and_numbers_are_typed()
    {
        var recipe = Bind(Minimal.Replace("step: step.produce", "step: { use: step.produce, with: { a: on, b: yes, c: no, d: true, e: 12, f: 1.5, g: '7', h: ~ } }", StringComparison.Ordinal));
        var with = recipe.States["produce"].Step!.With;

        Assert.Equal(JsonValueKind.String, with["a"].Literal!.Value.ValueKind);
        Assert.Equal("on", with["a"].Literal!.Value.GetString());
        Assert.Equal("yes", with["b"].Literal!.Value.GetString());
        Assert.Equal("no", with["c"].Literal!.Value.GetString());
        Assert.Equal(JsonValueKind.True, with["d"].Literal!.Value.ValueKind);
        Assert.Equal(12, with["e"].Literal!.Value.GetInt32());
        Assert.Equal(1.5, with["f"].Literal!.Value.GetDouble());
        Assert.Equal("7", with["g"].Literal!.Value.GetString());
        Assert.Null(with["h"].Literal);
        Assert.Equal("step.completed", recipe.Transitions[0].On);
    }

    [Fact, Trait("Lane", "unit")]
    public void Hex_and_octal_integers_of_64_bits_read_as_their_value_not_a_negative_number()
    {
        var recipe = Bind(Minimal.Replace("step: step.produce", "step: { use: step.produce, with: { a: 0xFFFFFFFFFFFFFFFF, b: 0o1777777777777777777777, c: 0x10, d: 0o17 } }", StringComparison.Ordinal));
        var with = recipe.States["produce"].Step!.With;

        Assert.Equal(18446744073709551615d, with["a"].Literal!.Value.GetDouble());
        Assert.Equal(18446744073709551615d, with["b"].Literal!.Value.GetDouble());
        Assert.Equal(16, with["c"].Literal!.Value.GetInt32());
        Assert.Equal(15, with["d"].Literal!.Value.GetInt32());

        var edge = Bind(Minimal.Replace("step: step.produce", "step: { use: step.produce, with: { a: 0x7FFFFFFFFFFFFFFF, b: 0x8000000000000000 } }", StringComparison.Ordinal))
            .States["produce"].Step!.With;
        Assert.Equal(long.MaxValue, edge["a"].Literal!.Value.GetInt64());
        Assert.Equal(9223372036854775808d, edge["b"].Literal!.Value.GetDouble());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("    kind: active", "    kind: 7", "RCP-SCHEMA-002: 7 isn't active, wait or terminal at test.yaml:7:5 (states.produce.kind); write active, wait or terminal")]
    [InlineData("    kind: active", "    kind: -1", "RCP-SCHEMA-002: -1 isn't active, wait or terminal at test.yaml:7:5 (states.produce.kind); write active, wait or terminal")]
    [InlineData("    kind: active", "    kind: \"3\"", "RCP-SCHEMA-002: '3' isn't active, wait or terminal at test.yaml:7:5 (states.produce.kind); write active, wait or terminal")]
    [InlineData("    kind: active", "    kind: \"active, wait\"", "RCP-SCHEMA-002: 'active, wait' isn't active, wait or terminal at test.yaml:7:5 (states.produce.kind); write active, wait or terminal")]
    [InlineData("    kind: active", "    kind: Active", "RCP-SCHEMA-002: 'Active' isn't active, wait or terminal at test.yaml:7:5 (states.produce.kind); write active, wait or terminal")]
    [InlineData("then: park", "then: 9", "RCP-SCHEMA-002: 9 isn't park, escalate or retry at test.yaml:9:27 (states.produce.timeout.then); write park, escalate or retry")]
    public void An_enum_takes_exactly_one_of_its_names_never_a_number_or_a_list(string from, string to, string line)
    {
        var lines = Lines(Minimal.Replace(from, to, StringComparison.Ordinal));

        Assert.Contains(line, lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reader_finding_doesnt_hide_the_schema_errors_found_after_binding()
    {
        var yaml = Minimal
            .Replace("    next: \"Produce the Step output.\"\n", string.Empty, StringComparison.Ordinal)
            .Replace("to: done", "to: nowhere", StringComparison.Ordinal)
            + "colour: blue\n";

        var lines = Lines(yaml);

        Assert.Equal(
            [
                "RCP-SCHEMA-001: 'colour' isn't a field of the recipe at test.yaml:15:1 (colour); remove it; the recipe has schema, id, version, description, base, urgency, name, inputs, outputs, initial, states, transitions, loops, budget, evals, hash, displayName, why",
                "RCP-SCHEMA-002: transition to 'nowhere' names a state the recipe doesn't declare at test.yaml:14:22 (transitions[0].to); name one of the recipe's states: produce, done",
                "RCP-SCHEMA-001: active state 'produce' has no 'next' at test.yaml:6:3 (states.produce.next); an active state has step, timeout and next, and may have loop and budget",
            ],
            lines);
        Assert.Null(Reader.Read(yaml, "test.yaml").Document);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_refused_state_isnt_then_reported_as_undeclared()
    {
        var yaml = Minimal.Replace(
            "  produce:\n    kind: active\n    step: step.produce\n    timeout: { after: 4h, then: park }\n    next: \"Produce the Step output.\"\n",
            "  produce: 5\n", StringComparison.Ordinal) + "colour: blue\n";

        var lines = Lines(yaml);

        Assert.Equal(2, lines.Count);
        Assert.StartsWith("RCP-SCHEMA-002: a state is written as 5 at test.yaml:6:3 (states.produce)", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("RCP-SCHEMA-001: 'colour' isn't a field of the recipe", lines[1], StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("    timeout: { after: 4h, then: park }", "    timeout: 5")]
    [InlineData("schema: 1\n", "schema: 1\nschema: 1\n")]
    public void After_a_wrong_type_or_refused_yaml_the_schema_rules_wait(string from, string to)
    {
        var lines = Lines(Minimal.Replace(from, to, StringComparison.Ordinal).Replace("to: done", "to: nowhere", StringComparison.Ordinal));

        _ = Assert.Single(lines);
        Assert.DoesNotContain(lines, l => l.Contains("nowhere", StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("  done:\n", "  \"x.y\": ~\n  done:\n")]
    [InlineData("schema: 1\n", "schema: 1\nschema: 1\n")]
    public void An_unknown_field_beside_another_reader_finding_leaves_the_schema_rules_waiting(string from, string to)
    {
        var lines = Lines(Minimal.Replace(from, to, StringComparison.Ordinal).Replace("to: done", "to: nowhere", StringComparison.Ordinal) + "colour: blue\n");

        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.StartsWith("RCP-SCHEMA-001: 'colour' isn't a field of the recipe", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("nowhere", StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("  \"x.y\": ~\n")]
    [InlineData("  \"a[x]\": { kind: active, wat: 1 }\n")]
    [InlineData("  \"a[0\": { kind: active, wat: 1 }\n")]
    [InlineData("  \"a.next\": ~\n")]
    public void A_state_name_with_a_dot_or_bracket_is_reported_not_a_crash(string state)
    {
        var lines = Lines(Minimal.Replace("  done:\n", state + "  done:\n", StringComparison.Ordinal));

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Matches("^RCP-(SCHEMA-00[12]|NAME-001): ", l));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_refused_value_isnt_reported_again_as_missing_by_the_schema_rules()
    {
        var lines = Lines(Minimal.Replace("next: \"Produce the Step output.\"", "next: [a, b]", StringComparison.Ordinal) + "colour: blue\n");

        Assert.Equal(2, lines.Count);
        Assert.StartsWith("RCP-SCHEMA-002: [\"a\",\"b\"] isn't text at test.yaml:10:5 (states.produce.next)", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("RCP-SCHEMA-001: 'colour' isn't a field of the recipe", lines[1], StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(".inf")]
    [InlineData("-.inf")]
    [InlineData(".nan")]
    [InlineData("1e999")]
    [InlineData("0x1FFFFFFFFFFFFFFFF")]
    [InlineData("0o2000000000000000000000")]
    public void A_number_JSON_cant_hold_is_RCP_SCHEMA_002_as_written(string number)
    {
        var lines = Lines(Minimal.Replace("step: step.produce", "step: { use: step.produce, with: { a: " + number + " } }", StringComparison.Ordinal));

        Assert.Equal(
            [$"RCP-SCHEMA-002: '{number}' is a number JSON can't hold at test.yaml:8:40 (states.produce.step.with.a); write a finite number, or quote it to make it text"],
            lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_node_position_is_recorded_as_written()
    {
        var positions = Reader.Read(Minimal, "test.yaml").Document!.Source.Positions;

        Assert.Equal(new SourcePosition(1, 1), positions.Locate(string.Empty));
        Assert.Equal(new SourcePosition(2, 1), positions.Locate("id"));
        Assert.Equal(new SourcePosition(6, 3), positions.Locate("states.produce"));
        Assert.Equal(new SourcePosition(8, 5), positions.Locate("states.produce.step"));
        Assert.Equal(new SourcePosition(9, 16), positions.Locate("states.produce.timeout.after"));
        Assert.Equal(new SourcePosition(15, 5), positions.Locate("transitions[0]"));
        Assert.Equal(new SourcePosition(15, 7), positions.Locate("transitions[0].from"));
        Assert.Equal(new SourcePosition(15, 88), positions.Locate("transitions[0].produces[0]"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_short_forms_long_children_locate_at_the_short_form()
    {
        var positions = Reader.Read(Minimal, "test.yaml").Document!.Source.Positions;

        Assert.Equal(positions.Locate("states.produce.step"), positions.Locate("states.produce.step.use"));
        Assert.Equal(positions.Locate("transitions[0].from"), positions.Locate("transitions[0].from[0]"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_short_form_normalizes_to_the_long_form()
    {
        var recipe = Reader.Read(RecipeFixtures.Read("valid/short-forms.yaml"), "short-forms.yaml").Document!.Recipe;
        var review = recipe.States["review"];

        Assert.Equal([new RecipeInput(FactKey.From("fact:item"), Label: "Item", Type: "item", Kind: "item", Generate: null)], recipe.Inputs);
        Assert.Equal("step.produce", recipe.States["produce"].Step!.Use.Value);
        Assert.Empty(recipe.States["produce"].Step!.With);
        Assert.Equal("input.context.retrieve", Assert.Single(review.Step!.Inputs).Use.Value);
        Assert.Equal(["produce"], recipe.Transitions[0].From);
        Assert.Equal("gate.budget.available", Assert.Single(recipe.Transitions[0].Gates).Use.Value);
        Assert.Null(recipe.Transitions[0].Gates[0].Threshold);
        Assert.Equal("effect.notify.desk", Assert.Single(recipe.Transitions[0].Effects).Use.Value);
        Assert.Empty(recipe.Transitions[1].Produces);
        Assert.Equal(200_000, recipe.States["produce"].Budget!.Tokens);
        Assert.Equal(2_000_000, recipe.Budget!.Tokens);
        Assert.True(review.Step.With["strict"].Literal!.Value.GetBoolean());
        Assert.Equal("write it", recipe.States["running"].WaitFor!.Child!.Inputs[FactKey.From("fact:spec")].Literal!.Value.GetString());
        Assert.Equal(LoopSpec.DefaultNoProgress, recipe.Loops["revisions"].NoProgress);
        Assert.Empty(recipe.Evals);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_violation_is_reported_not_just_the_first()
    {
        var yaml = Minimal
            .Replace("kind: active", "kind: activ", StringComparison.Ordinal)
            .Replace("next: \"Produce the Step output.\"", "next: \"Produce the Step output.\"\n    colour: blue", StringComparison.Ordinal)
            .Replace("after: 4h", "after: 4 hours", StringComparison.Ordinal)
            .Replace("version: 1", "version: one", StringComparison.Ordinal);

        var findings = Reader.Read(yaml, "test.yaml");

        Assert.Null(findings.Document);
        Assert.Equal(
            [
                "RCP-SCHEMA-002: 'one' isn't a whole number at test.yaml:3:1 (version); write a whole number",
                "RCP-SCHEMA-002: 'activ' isn't active, wait or terminal at test.yaml:7:5 (states.produce.kind); write active, wait or terminal",
                "RCP-DUR-001: '4 hours' isn't a duration ('10m', '4h', '2d', at least 1m) at test.yaml:9:16 (states.produce.timeout.after); write a whole number and m, h or d, at least 1m ('10m', '4h', '2d')",
                "RCP-SCHEMA-001: 'colour' isn't a field of a state at test.yaml:11:5 (states.produce.colour); remove it; a state has kind, step, waitFor, timeout, loop, budget, next, outcome, why, displayName, onOutcome, model",
            ],
            findings.Findings.Select(f => f.ToLine()),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_required_field_is_reported_where_its_parent_starts()
    {
        var lines = Lines(Minimal.Replace("initial: produce\n", string.Empty, StringComparison.Ordinal).Replace("to: done, ", string.Empty, StringComparison.Ordinal));

        Assert.Equal(
            [
                "RCP-SCHEMA-001: a transition has no 'to' at test.yaml:14:5 (transitions[0].to); add 'to': text",
                "RCP-SCHEMA-001: the recipe has no 'initial' at test.yaml:1:1 (initial); add 'initial': text",
            ],
            lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_list_written_where_a_mapping_belongs_is_a_wrong_type()
    {
        var lines = Lines(Minimal.Replace("timeout: { after: 4h, then: park }", "timeout: [4h, park]", StringComparison.Ordinal));

        Assert.Equal(["RCP-SCHEMA-002: a timeout is written as a list at test.yaml:9:5 (states.produce.timeout); write a timeout as a mapping"], lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bad_token_count_and_a_literal_list_of_mappings_are_wrong_types()
    {
        var lines = Lines(Minimal
            .Replace("    next: \"Produce", "    budget: { tokens: lots }\n    next: \"Produce", StringComparison.Ordinal)
            .Replace("step: step.produce", "step: { use: step.produce, with: { files: [{ a: 1, b: 2 }] } }", StringComparison.Ordinal));

        Assert.Equal(
            [
                "RCP-SCHEMA-002: the list 'files' holds more than strings, numbers and bools at test.yaml:8:40 (states.produce.step.with.files); a literal list holds only strings, numbers and bools; use { fact } or { profile } for anything else",
                "RCP-SCHEMA-002: 'lots' isn't a token count at test.yaml:10:15 (states.produce.budget.tokens); write a whole number of at least 1, or one with k or M ('1500', '200k', '2M')",
            ],
            lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_one_key_mapping_in_a_step_with_list_stays_for_the_block_rule_to_refuse()
    {
        var yaml = Minimal.Replace("step: step.produce", "step: { use: step.produce, with: { files: [a: 1] } }", StringComparison.Ordinal);

        var document = RecipeComposition.ReadClean(yaml, "test.yaml");
        var step = TestBlocks.Step("step.produce") with { Parameters = [new ParameterSpec("files", ParameterType.List, Required: false, IsCapabilityField: false)] };
        var errors = RecipeComposition.Errors(document, new BlockCatalog([step], []), InMemoryChildRecipes.None);

        Assert.Equal(
            "RCP-BLOCK-003: parameter 'files' of 'step.produce' takes a list of strings, numbers or flags, not a list with an object or a list in it at test.yaml:8:40 (states.produce.step.with.files); give it a list of strings, numbers or flags",
            Assert.Single(errors, static l => l.StartsWith("RCP-BLOCK-003", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_one_key_mapping_in_a_child_input_list_is_still_a_wrong_type()
    {
        var yaml = Minimal.Replace(
            "    step: step.produce\n",
            "    waitFor: { child: { recipe: workflow.step, inputs: { \"fact:x\": [ok: yes] }, onParked: propagate } }\n",
            StringComparison.Ordinal);

        var line = Assert.Single(Lines(yaml), l => l.StartsWith("RCP-SCHEMA-002", StringComparison.Ordinal));
        Assert.Contains("holds more than strings, numbers and bools", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_that_isnt_a_mapping_is_a_wrong_type() =>
        Assert.Equal(
            ["RCP-SCHEMA-002: the recipe isn't a mapping at test.yaml:1:1 (); write the recipe as a YAML mapping with schema, id, version, initial, states and transitions"],
            Lines("- just\n- a list\n"));
}
