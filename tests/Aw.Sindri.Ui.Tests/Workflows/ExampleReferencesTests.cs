using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3944: which example texts name a step the new step can't read. A template field reads only {{node.field}}; a condition's "when" also reads bare words the way the
/// condition tokenizer does (Aw.Sindri.Core/Graph/Expressions/Tokenizer.cs): a "string", a number, and, or, not, true, false, null and exists( are not references.
/// The step the new one may read here is "tell".
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ExampleReferencesTests
{
    private static readonly HashSet<string> _tell = new(["tell"], StringComparer.Ordinal);

    [Theory]
    [InlineData("agent.drifted", true)]
    [InlineData("agent", true)]
    [InlineData("not agent.drifted", true)]
    [InlineData("tell.ok and agent.drifted", true)]
    [InlineData("{{agent.drifted}} == true", true)]
    [InlineData("tell.ok", false)]
    [InlineData("trigger.ref != null", false)]
    [InlineData("exists(item.title) and itemIndex > 2", false)]
    [InlineData("\"agent.drifted\" == tell.text", false)]
    [InlineData("\"say \\\"agent.x\\\"\" == workflow.name", false)]
    [InlineData("tell.count >= -1.5 or false", false)]
    [InlineData("not (tell.ok == true)", false)]
    public void A_condition_reads_bare_and_braced_references_and_nothing_else(string when, bool foreign) =>
        Assert.Equal(foreign, ExampleReferences.NamesStepNotUpstream("condition", "when", when, _tell));

    [Theory]
    [InlineData("{{agent.summary}}", true)]
    [InlineData("{{ tell.summary }} and {{trigger.ref}}", false)]
    [InlineData("agent.drifted", false)]
    public void A_template_field_reads_only_braced_references(string text, bool foreign) =>
        Assert.Equal(foreign, ExampleReferences.NamesStepNotUpstream("notify", "body", text, _tell));

    [Fact]
    public void Another_field_of_a_condition_reads_only_braced_references() =>
        Assert.False(ExampleReferences.NamesStepNotUpstream("condition", "note", "agent.drifted", _tell));

    // #4272: the hint a new step's field shows names the steps the new one can't read as "<step id>", and keeps the rest of the example.
    [Theory]
    [InlineData("notify", "body", "{{agent.summary}}", "{{<step id>.summary}}")]
    [InlineData("notify", "body", "{{ agent.summary }} again", "{{ <step id>.summary }} again")]
    [InlineData("notify", "body", "{{ tell.summary }} {{trigger.ref}}", "{{ tell.summary }} {{trigger.ref}}")]
    [InlineData("notify", "title", "Drifted", "Drifted")]
    [InlineData("condition", "when", "agent.drifted", "<step id>.drifted")]
    [InlineData("condition", "when", "agent.drifted and tell.ok", "<step id>.drifted and tell.ok")]
    [InlineData("condition", "when", "{{agent.x}} == true", "{{<step id>.x}} == true")]
    [InlineData("condition", "note", "agent.drifted", "agent.drifted")]
    public void A_hint_names_a_step_the_new_one_cant_read_as_a_step_id(string type, string field, string text, string hint) =>
        Assert.Equal(hint, ExampleReferences.Hint(type, field, text, _tell));
}
