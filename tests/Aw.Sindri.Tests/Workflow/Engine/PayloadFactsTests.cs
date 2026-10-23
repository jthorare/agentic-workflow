using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The recipe's 'payload' facts (run23): which a Step writes, what an agent is told about their shape, and what a report may hold.</summary>
[Lane(TestLane.Unit)]
public sealed class PayloadFactsTests
{
    private static readonly FactKey _design = FactKey.From("fact:design");
    private static readonly FactKey _notes = FactKey.From("fact:notes");
    private static readonly FactKey _packet = FactKey.From("fact:designPacket");

    [Theory]
    [InlineData("""{"a":1}""")]
    [InlineData("[]")]
    [InlineData("  {\n \"sections\": [] }\n")]
    public void A_JSON_object_or_array_is_a_payload(string content) =>
        Assert.Null(PayloadFacts.Violation(_design, Encoding.UTF8.GetBytes(content)));

    [Theory]
    [InlineData("Here is the design.")]
    [InlineData("# Design\n\nUse a queue.")]
    [InlineData("\"just a string\"")]
    [InlineData("42")]
    [InlineData("{\"cut off\":")]
    [InlineData("")]
    public void Anything_else_is_refused_naming_the_fact_and_the_fix(string content)
    {
        var why = PayloadFacts.Violation(_design, Encoding.UTF8.GetBytes(content));

        Assert.NotNull(why);
        Assert.Contains("fact:design", why, StringComparison.Ordinal);
        Assert.Contains("JSON object or array", why, StringComparison.Ordinal);
    }

    [Fact]
    public void The_recipe_declares_its_payload_facts_in_inputs_and_outputs_and_nothing_else()
    {
        var recipe = Recipe(
            inputs: [new RecipeInput(FactKey.From("fact:revision"), "Revision", "payload", "payload", Generate: null)],
            outputs: [new RecipeOutput(_design, "Design", "payload", "draft"), new RecipeOutput(_notes, "Notes", "text", "draft"), new RecipeOutput(_packet, "Packet", "packet", "draft"), new RecipeOutput(FactKey.From("fact:plain"), "Plain", Type: null, "draft")]);

        var declared = PayloadFacts.DeclaredIn(recipe);

        Assert.Equal(2, declared.Count);
        Assert.Contains(FactKey.From("fact:revision"), declared);
        Assert.Contains(_design, declared);
    }

    [Fact]
    public void A_Step_writes_the_payload_outputs_its_descriptor_declares()
    {
        var recipe = Recipe([], [new RecipeOutput(_design, "Design", "payload", "draft"), new RecipeOutput(_notes, "Notes", "text", "draft")]);
        var descriptor = Descriptor(_notes, _design);

        Assert.Equal([_design], PayloadFacts.OutputsOf(recipe, descriptor));
    }

    [Fact]
    public void The_shape_line_names_the_fact_and_says_JSON_not_prose()
    {
        var line = PayloadFacts.Instruction(_design);

        Assert.Contains("fact:design", line, StringComparison.Ordinal);
        Assert.Contains("JSON object or array", line, StringComparison.Ordinal);
        Assert.Contains("not prose", line, StringComparison.Ordinal);
    }

    [Fact]
    public void The_design_fact_says_its_spec_field_holds_the_Markdown_and_other_facts_get_no_hint()
    {
        var line = PayloadFacts.Instruction(_design);

        Assert.Contains("\"spec\": \"<the whole design as Markdown", line, StringComparison.Ordinal);
        Assert.Contains("\"sections\"", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Shape:", PayloadFacts.Instruction(_notes), StringComparison.Ordinal);
    }

    [Fact]
    public void Shapes_follow_the_Steps_own_instructions_and_a_Step_without_either_has_none()
    {
        var lines = PayloadFacts.WithShapes(["Goal: draft."], [_design]);

        Assert.Equal(["Goal: draft.", PayloadFacts.Instruction(_design)], lines);
        Assert.Equal(["Goal: draft."], PayloadFacts.WithShapes(["Goal: draft."], []));
        Assert.Null(PayloadFacts.WithShapes(instructions: null, []));
    }

    private static BlockDescriptor Descriptor(params FactKey[] outputs) =>
        new(BlockId.From("step.test.draft"), BlockKind.Step, 1, [], [.. outputs.Select(static o => new OutputSpec(o, []))], [OutcomeName.From("done")], Capability.None, [], Rung: null, DeskCapable: false);

    private static Recipe Recipe(IReadOnlyList<RecipeInput> inputs, IReadOnlyList<RecipeOutput> outputs) =>
        new(1, RecipeId.From("workflow.test"), 1, Description: null, Base: null, Urgency: "work", Name: null, inputs, outputs, "draft",
            new Dictionary<string, RecipeState>(StringComparer.Ordinal), [], new Dictionary<string, LoopSpec>(StringComparer.Ordinal), Budget: null, Evals: [], Hash: null);
}
