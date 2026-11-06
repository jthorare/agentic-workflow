using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>
/// RCP-URG-001, RCP-OUT-001/002/003 and RCP-TYPE-001/002/003 (spec §3.2, §3.3, §9.1 "Added (UI E-21, E-22)"). The
/// recipes mirror invalid/RCP-URG-001.yaml and its siblings.
/// </summary>
public sealed class DeclarationRulesTests
{
    private static readonly (string, RecipeState)[] _produceThenDone = [("produce", Active("step.produce")), ("done", Terminal())];

    private static Recipe Base(IReadOnlyList<RecipeOutput>? outputs = null, IReadOnlyList<RecipeInput>? inputs = null, string urgency = "work", string? name = null) =>
        Of("produce", _produceThenDone, [Go("produce", "done", when: Outcome("done"))]) with
        {
            Urgency = urgency,
            Name = name,
            Outputs = outputs ?? [],
            Inputs = inputs ?? [In("fact:item", "item", "item")],
        };

    private static RecipeOutput Out(string key, string from, string? type = "file") => new(FactKey.From(key), Label: null, type, from);

    private static RecipeInput In(string key, string? type = null, string? kind = "text", string? generate = null) => new(FactKey.From(key), Label: null, type, kind, generate);

    private static Located At(params (string Path, int Line, int Column)[] positions) => new("r.yaml", positions);

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_that_declares_nothing_has_no_findings()
    {
        Assert.Empty(CheckDeclarations(Base()));
        Assert.Empty(CheckDeclarations(Base(
            outputs: [Out("fact:output", "produce")],
            inputs: [In("fact:item", "item", "item"), In("fact:note", type: null, kind: "text", generate: "note {start:date} {start:time}"), In("fact:p", "payload", "webhook"), In("fact:l", type: null, kind: "link")],
            name: "{fact:item} {fact:note} {start:date} {{x}}",
            urgency: "incident")));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_urgency_other_than_incident_triage_or_work_is_reported()
    {
        Assert.Equal(
            ["RCP-URG-001: urgency 'urgent' isn't one of incident, triage, work at r.yaml:4:1 (urgency); use incident, triage or work, or remove urgency (the default is work)"],
            CheckDeclarations(Base(urgency: "urgent"), At(("urgency", 4, 1))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_from_a_state_the_recipe_doesnt_declare_is_reported()
    {
        Assert.Equal(
            ["RCP-OUT-001: output 'fact:output' comes from state 'draft', which the recipe doesn't declare at r.yaml:5:48 (outputs[0].from); name one of its states: produce, done"],
            CheckDeclarations(Base(outputs: [Out("fact:output", "draft")]), At(("outputs[0].from", 5, 48))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_from_a_terminal_state_is_reported()
    {
        Assert.Equal(
            ["RCP-OUT-002: output 'fact:output' comes from terminal state 'done', which produces nothing at r.yaml:5:48 (outputs[0].from); name the state that produces it"],
            CheckDeclarations(Base(outputs: [Out("fact:output", "done")]), At(("outputs[0].from", 5, 48))).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("parked")]
    [InlineData("escalated")]
    [InlineData("taken-over")]
    [InlineData("cancelled")]
    public void An_output_from_an_engine_owned_state_is_reported_as_OUT_002_not_OUT_001(string state)
    {
        Assert.Equal(
            [$"RCP-OUT-002: output 'fact:output' comes from '{state}', a state the engine owns, not one of the recipe's at r.yaml:5:48 (outputs[0].from); name the recipe state that produces it"],
            CheckDeclarations(Base(outputs: [Out("fact:output", state)]), At(("outputs[0].from", 5, 48))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_nothing_in_its_state_records_is_reported_with_what_the_state_does_record()
    {
        Assert.Equal(
            ["RCP-OUT-002: output 'fact:report' comes from state 'produce', but nothing there records it (step 'step.produce' records fact:output) at r.yaml:5:48 (outputs[0].from); have the state's Step, a transition leaving it ('produces') or its wait record 'fact:report', or name the state that does"],
            CheckDeclarations(Base(outputs: [Out("fact:report", "produce")]), At(("outputs[0].from", 5, 48))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_a_leaving_transition_produces_or_an_answer_wait_records_passes()
    {
        var recipe = Of(
            "produce",
            [("produce", Active("step.produce")), ("asked", WaitForAnswer("accept")), ("done", Terminal())],
            [
                Go("produce", "asked", when: Outcome("done"), produces: ["fact:report"]),
                Go("asked", "done", "wait.satisfied", Outcome("answered")),
            ]) with
        {
            Outputs = [Out("fact:report", "produce"), Out("fact:output", "produce"), Out("fact:answer", "asked", "decision")],
        };

        Assert.Empty(CheckDeclarations(recipe));
    }

    private static Recipe Waiting(RecipeState wait, string key, params string[] produces) =>
        Of(
            "produce",
            [("produce", Active("step.produce")), ("wait", wait), ("done", Terminal())],
            [Go("produce", "wait", when: Outcome("done")), Go("wait", "done", "wait.satisfied", produces: produces)]) with
        {
            Outputs = [Out(key, "wait")],
        };

    [Fact, Trait("Lane", "unit")]
    public void An_output_a_waited_for_event_carries_passes_and_one_it_does_not_names_the_event()
    {
        Assert.Empty(CheckDeclarations(Waiting(WaitForEvent("item.routed"), "fact:stepSpec")));
        Assert.Equal(
            ["RCP-OUT-002: output 'fact:report' comes from state 'wait', but nothing there records it (event 'item.routed' records fact:stepSpec) at r.yaml:5:48 (outputs[0].from); have the state's Step, a transition leaving it ('produces') or its wait record 'fact:report', or name the state that does"],
            CheckDeclarations(Waiting(WaitForEvent("item.routed"), "fact:report"), At(("outputs[0].from", 5, 48))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_from_a_resource_wait_errors_unless_a_leaving_transition_produces_it()
    {
        Assert.Empty(CheckDeclarations(Waiting(WaitForResource(ResourceKind.Heavy), "fact:report", "fact:report")));
        Assert.Equal(
            ["RCP-OUT-002: output 'fact:report' comes from state 'wait', but nothing there records it (resource records nothing) at r.yaml:5:48 (outputs[0].from); have the state's Step, a transition leaving it ('produces') or its wait record 'fact:report', or name the state that does"],
            CheckDeclarations(Waiting(WaitForResource(ResourceKind.Heavy), "fact:report"), At(("outputs[0].from", 5, 48))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_from_a_state_whose_block_or_event_is_unknown_is_left_to_the_block_rules()
    {
        var unknownStep = Of("produce", [("produce", Active("step.nope")), ("done", Terminal())], [Go("produce", "done", when: Outcome("done"))]) with
        {
            Outputs = [Out("fact:report", "produce")],
        };

        Assert.Empty(CheckDeclarations(unknownStep));
        Assert.Empty(CheckDeclarations(Waiting(WaitForEvent("no.such"), "fact:report")));
    }

    private static Recipe WaitingOnKid() => Of(
        "running",
        [("running", WaitForChild("workflow.kid")), ("done", Terminal())],
        [Go("running", "done", "wait.satisfied", AnyOf(Outcome("resolved"), Outcome("cancelled")))]) with
    {
        Outputs = [Out("fact:result", "running")],
    };

    private static Located WithKid(string declared) => At(("outputs[0].from", 5, 48)) with
    {
        Children = new InMemoryChildRecipes().With(Child(
            Of("produce", _produceThenDone, [Go("produce", "done", when: Outcome("done"))], inputs: [], id: "workflow.kid") with { Outputs = [OutputOf(declared, "produce")] })),
    };

    [Fact, Trait("Lane", "unit")]
    public void An_output_from_a_child_wait_is_recorded_by_the_childs_outputs() =>
        Assert.Empty(CheckDeclarations(WaitingOnKid(), WithKid("fact:result")));

    [Fact, Trait("Lane", "unit")]
    public void An_output_the_child_doesnt_declare_is_RCP_OUT_002() =>
        Assert.Equal(
            ["RCP-OUT-002: output 'fact:result' comes from state 'running', but nothing there records it (child 'workflow.kid' records fact:other) at r.yaml:5:48 (outputs[0].from); have the state's Step, a transition leaving it ('produces') or its wait record 'fact:result', or name the state that does"],
            CheckDeclarations(WaitingOnKid(), WithKid("fact:other")).Lines());

    [Fact, Trait("Lane", "unit")]
    public void An_unresolved_child_gives_no_OUT_002() =>
        Assert.Empty(CheckDeclarations(WaitingOnKid(), At(("outputs[0].from", 5, 48))));

    [Fact, Trait("Lane", "unit")]
    public void A_duplicate_output_key_and_an_output_key_that_is_an_input_key_are_reported()
    {
        var recipe = Base(
            outputs: [Out("fact:output", "produce"), Out("fact:output", "produce"), Out("fact:item", "produce")],
            inputs: [In("fact:item")]);

        Assert.Equal(
            [
                "RCP-OUT-003: output key 'fact:output' is declared twice (first at outputs[0]) at r.yaml:6:10 (outputs[1].key); remove the duplicate, or give it its own key",
                "RCP-OUT-003: output key 'fact:item' is also an input key at r.yaml:7:10 (outputs[2].key); use another key for the output, since an input is already a fact the recipe starts with",
            ],
            CheckDeclarations(recipe, At(("outputs[1].key", 6, 10), ("outputs[2].key", 7, 10))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_type_that_isnt_a_fact_type_is_reported_on_inputs_and_outputs()
    {
        var recipe = Base(outputs: [Out("fact:output", "produce", "document")], inputs: [In("fact:item", "ticket")]);

        Assert.Equal(
            [
                "RCP-TYPE-001: input 'fact:item' has type 'ticket', which isn't one of text, file, item, link, scope-map, packet, evidence, decision, payload, findings at r.yaml:4:30 (inputs[0].type); use one of them",
                "RCP-TYPE-001: output 'fact:output' has type 'document', which isn't one of text, file, item, link, scope-map, packet, evidence, decision, payload, findings at r.yaml:6:30 (outputs[0].type); use one of them",
            ],
            CheckDeclarations(recipe, At(("inputs[0].type", 4, 30), ("outputs[0].type", 6, 30))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_kind_that_isnt_a_subject_kind_is_reported()
    {
        Assert.Equal(
            ["RCP-TYPE-002: input 'fact:item' has kind 'ticket', which isn't one of item, linear-project, webhook, file, text, link, packet, payload at r.yaml:4:40 (inputs[0].kind); use one of them"],
            CheckDeclarations(Base(inputs: [In("fact:item", "item", "ticket")]), At(("inputs[0].kind", 4, 40))).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("file", "item", "RCP-TYPE-003: input 'fact:item' has kind 'item' but type 'file'; kind item pairs with type item at r.yaml:4:40 (inputs[0].kind); set type to item, or change kind")]
    [InlineData("text", "item", "RCP-TYPE-003: input 'fact:item' has kind 'item' but type 'text'; kind item pairs with type item at r.yaml:4:40 (inputs[0].kind); set type to item, or change kind")]
    [InlineData("text", "webhook", "RCP-TYPE-003: input 'fact:item' has kind 'webhook' but type 'text'; kind webhook pairs with type payload at r.yaml:4:40 (inputs[0].kind); set type to payload, or change kind")]
    [InlineData("text", "packet", "RCP-TYPE-003: input 'fact:item' has kind 'packet' but type 'text'; kind packet pairs with type packet at r.yaml:4:40 (inputs[0].kind); set type to packet, or change kind")]
    [InlineData("text", "payload", "RCP-TYPE-003: input 'fact:item' has kind 'payload' but type 'text'; kind payload pairs with type payload at r.yaml:4:40 (inputs[0].kind); set type to payload, or change kind")]
    public void A_kind_and_type_that_dont_pair_are_reported(string? type, string kind, string expected) =>
        Assert.Equal([expected], CheckDeclarations(Base(inputs: [In("fact:item", type, kind)]), At(("inputs[0].kind", 4, 40))).Lines());

    [Theory, Trait("Lane", "unit")]
    [InlineData("packet", "packet")]
    [InlineData("payload", "payload")]
    [InlineData(null, "packet")]
    public void The_packet_and_payload_kinds_are_known_and_pair_with_their_own_type(string? type, string kind) =>
        Assert.Empty(CheckDeclarations(Base(inputs: [In("fact:item", type, kind)])));

    [Fact, Trait("Lane", "unit")]
    public void An_unpaired_check_is_skipped_for_an_entry_with_a_bad_type_or_kind()
    {
        var recipe = Base(inputs: [In("fact:a", "ticket", "item"), In("fact:b", "file", "ticket")]);

        Assert.True(CheckDeclarations(recipe).Select(f => f.Rule).SequenceEqual(["RCP-TYPE-001", "RCP-TYPE-002"], StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_input_that_declares_no_kind_is_reported_and_the_type_is_not_enough()
    {
        Assert.Equal(
            ["RCP-INPUT-001: input 'fact:item' declares no kind at r.yaml:5:9 (inputs[0]); add kind: item | link | linear-project | webhook | file | text | packet | payload, paired with its type (item/item, file/file, text/text, link/link, packet/packet; payload, linear-project and webhook pair with payload)"],
            CheckDeclarations(Base(inputs: [In("fact:item", "item", kind: null)]), At(("inputs[0]", 5, 9))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_with_no_input_is_reported()
    {
        Assert.Equal(
            ["RCP-INPUT-002: the recipe declares no input at r.yaml:1:1 (inputs); declare at least one input; a scheduled recipe gives it generate: \"<template>\""],
            CheckDeclarations(Base(inputs: []), At(("inputs", 1, 1))).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_name_is_reported(string name) =>
        Assert.Equal(
            ["RCP-NAME-002: the name template is empty or longer than 200 characters at r.yaml:4:1 (name); write literal text with {fact:<input>} or {start:date|time}"],
            CheckDeclarations(Base(name: name), At(("name", 4, 1))).Lines());

    [Fact, Trait("Lane", "unit")]
    public void A_name_or_generate_template_over_200_characters_is_reported_and_200_passes()
    {
        Assert.Empty(CheckDeclarations(Base(name: new string('a', 200))));
        Assert.Equal(
            ["RCP-NAME-002: the name template is empty or longer than 200 characters at r.yaml:4:1 (name); write literal text with {fact:<input>} or {start:date|time}"],
            CheckDeclarations(Base(name: new string('a', 201)), At(("name", 4, 1))).Lines());
        Assert.Equal(
            ["RCP-NAME-002: the generate template is empty or longer than 200 characters at r.yaml:5:30 (inputs[0].generate); write literal text with {start:date} or {start:time}"],
            CheckDeclarations(Base(inputs: [In("fact:note", generate: new string('a', 201))]), At(("inputs[0].generate", 5, 30))).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{owner} sweep", "unknown placeholder '{owner}'")]
    [InlineData("{fact:} sweep", "unknown placeholder '{fact:}'")]
    [InlineData("{start:zone}", "unknown placeholder '{start:zone}'")]
    [InlineData("{start:date", "unbalanced '{' or '}'")]
    [InlineData("done}", "unbalanced '{' or '}'")]
    [InlineData("{{x}", "unbalanced '{' or '}'")]
    public void An_unknown_placeholder_or_an_unbalanced_brace_is_reported_once_per_template(string name, string what) =>
        Assert.Equal(
            [$"RCP-NAME-003: {what} at r.yaml:4:1 (name); use {{fact:<input>}}, {{start:date}} or {{start:time}}, and write {{{{ or }}}} for a literal brace"],
            CheckDeclarations(Base(name: name), At(("name", 4, 1))).Lines());

    [Theory, Trait("Lane", "unit")]
    [InlineData("line one\nline two")]
    [InlineData("tab\there")]
    public void A_control_character_in_a_name_or_generate_template_is_RCP_NAME_003(string text)
    {
        Assert.Equal(
            ["RCP-NAME-003: a control character (a line break or tab) in the template at r.yaml:4:1 (name); keep the template on one line, without control characters"],
            CheckDeclarations(Base(name: text), At(("name", 4, 1))).Lines());
        Assert.Equal(
            ["RCP-NAME-003: a control character (a line break or tab) in the template at r.yaml:5:30 (inputs[0].generate); keep the template on one line, without control characters"],
            CheckDeclarations(Base(inputs: [In("fact:note", generate: text)]), At(("inputs[0].generate", 5, 30))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_name_placeholder_that_names_no_declared_input_is_reported_once_per_input()
    {
        Assert.Equal(
            ["RCP-NAME-004: '{fact:scope}' names no declared input at r.yaml:4:1 (name); declare fact:scope under inputs or remove the placeholder"],
            CheckDeclarations(Base(name: "{fact:scope} {fact:scope} {fact:item}"), At(("name", 4, 1))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_generate_template_may_not_read_inputs()
    {
        Assert.Equal(
            ["RCP-NAME-005: generate may not read inputs ('{fact:item}') at r.yaml:5:47 (inputs[0].generate); use only literals, {start:date} and {start:time}"],
            CheckDeclarations(Base(inputs: [In("fact:note", generate: "{fact:item} run")]), At(("inputs[0].generate", 5, 47))).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("item", "item")]
    [InlineData("file", "file")]
    [InlineData("link", "link")]
    [InlineData("webhook", "payload")]
    [InlineData("linear-project", "payload")]
    public void Generate_on_an_input_whose_kind_is_not_text_is_reported(string kind, string type) =>
        Assert.Equal(
            [$"RCP-NAME-006: generate on an input of kind '{kind}' at r.yaml:5:47 (inputs[0].generate); only a text input can be generated; supply this input at start"],
            CheckDeclarations(Base(inputs: [In("fact:item", type, kind, generate: "run")]), At(("inputs[0].generate", 5, 47))).Lines());
}
