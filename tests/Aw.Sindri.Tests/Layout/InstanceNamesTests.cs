using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Layout;

/// <summary>Instance names and generated inputs (spec section 3.11): the closed template, rendered once from the start time and the facts.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceNamesTests
{
    private static readonly FactKey _scope = FactKey.From("fact:scope");
    private static readonly ItemKey _item = ItemKey.From("ITM-01JL2");
    private static readonly DateTimeOffset _start = new(2026, 10, 15, 9, 5, 0, TimeSpan.FromHours(2));   // 07:05 in UTC

    private static InterpretedRecipe Recipe(string? name, string kind = "text", string? type = null, string? fileName = null) =>
        StatusCases.Recipe with
        {
            Recipe = EngineFixture.Step with { Name = name, Inputs = [new RecipeInput(_scope, "Scope", type, kind, Generate: null)] },
            Inputs = fileName is null ? [] : new Dictionary<FactKey, InputFact> { [_scope] = new(fileName, EngineFixture.Fact("x")) },
        };

    private static string? Render(string? name, string? content, string kind = "text", string? fileName = null) =>
        InstanceNames.Render(Recipe(name, kind, fileName: fileName), _item, new Dictionary<FactKey, string?> { [_scope] = content }, _start);

    [Theory]
    [InlineData("triage {start:date}", "triage 2026-10-15")]
    [InlineData("at {start:time} UTC", "at 07:05 UTC")]
    [InlineData("{start:date} {start:time}", "2026-10-15 07:05")]
    [InlineData("{{braces}} stay", "{braces} stay")]
    public void A_generate_template_is_its_literals_and_the_start_time_in_utc(string template, string expected) =>
        Assert.Equal(expected, InstanceNames.Generate(template, _start));

    [Theory]
    [InlineData("{fact:scope} sweep")]
    [InlineData("{owner}")]
    [InlineData("open { brace")]
    public void A_generate_template_that_reads_a_fact_or_is_malformed_renders_nothing(string template) =>
        Assert.Null(InstanceNames.Generate(template, _start));

    [Fact]
    public void A_recipe_without_a_name_renders_none() => Assert.Null(Render(name: null, "x"));

    [Fact]
    public void A_text_is_its_first_non_empty_line_beside_the_utc_date() =>
        Assert.Equal("Crash on launch · 2026-10-15", Render("{fact:scope} · {start:date}", "\n  Crash on launch  \nmore"));

    [Fact]
    public void A_fact_that_cant_be_read_is_the_inputs_bare_key_and_the_rest_stays() =>
        Assert.Equal("scope · 2026-10-15", Render("{fact:scope} · {start:date}", content: null));

    [Fact]
    public void A_value_is_cut_to_40_and_the_name_to_80_without_splitting_a_pair()
    {
        Assert.Equal(new string('a', 40) + "!", Render("{fact:scope}!", new string('a', 100)));
        Assert.Equal(new string('a', 39), Render("{fact:scope}", new string('a', 39) + "\U0001F600tail"));
        Assert.Equal(new string('b', 80), Render(new string('b', 150), "x"));
    }

    [Fact]
    public void Control_characters_become_spaces_inside_a_value()
    {
        var content = "one\ttwo";

        Assert.Equal("one two", Render("{fact:scope}", content));
    }

    [Fact]
    public void An_item_is_its_id_and_a_file_is_its_name() =>
        Assert.Equal(("ITM-01JL2", "spec.md"), (Render("{fact:scope}", content: null, kind: "item"), Render("{fact:scope}", content: null, kind: "file", fileName: "spec.md")));

    [Theory]
    [InlineData("""{"url":"https://example.com/p/9","title":"Design brief"}""", "Design brief")]
    [InlineData("https://example.com/docs/page", "example.com")]
    public void A_link_is_its_title_else_its_host(string content, string expected) => Assert.Equal(expected, Render("{fact:scope}", content, kind: "link"));

    [Theory]
    [InlineData("webhook", """{"id":"P-2211","number":8}""", "P-2211")]
    [InlineData("webhook", """{"number":8812}""", "8812")]
    [InlineData("webhook", """{"other":1}""", "scope")]
    [InlineData("payload", """{"id":"P-2211","number":8}""", "P-2211")]
    [InlineData("payload", """{"number":8812}""", "8812")]
    [InlineData("payload", """{"other":1}""", "scope")]
    [InlineData("packet", """{"id":"P-2211","number":8}""", "P-2211")]
    [InlineData("packet", """{"number":8812}""", "8812")]
    [InlineData("packet", """{"other":1}""", "scope")]
    [InlineData("linear-project", """{"id":"P-2211","number":8}""", "P-2211")]
    public void A_payload_is_its_id_else_its_number_else_the_bare_key(string kind, string content, string expected) => Assert.Equal(expected, Render("{fact:scope}", content, kind: kind));

    [Fact]
    public void The_facts_a_name_reads_are_listed_once_in_order_of_use() =>
        Assert.Equal([_scope], InstanceNames.FactsRead(Recipe("{fact:scope}/{fact:scope} {start:date}").Recipe));

    [Fact]
    public void Status_prints_the_name_first_and_marks_a_generated_input()
    {
        var view = StatusCases.View("desk-task");
        var generated = view.Task!.Inputs.ToDictionary(static i => i.Key, static i => i.Value with { Generated = true });

        var named = StatusLayout.Render(view with { Name = "Crash on launch · 2026-10-15", Task = view.Task with { Inputs = generated } }, StatusCases.Now).Split('\n');
        var plain = StatusLayout.Render(view, StatusCases.Now).Split('\n');

        Assert.StartsWith("  name      Crash on launch", named[1], StringComparison.Ordinal);
        Assert.EndsWith(" (generated)", named.First(static l => l.StartsWith("  inputs ", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.DoesNotContain(plain, static l => l.StartsWith("  name ", StringComparison.Ordinal) || l.EndsWith("(generated)", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_rendered_name_shows_the_instance_address()
    {
        var view = StatusCases.View("desk-task");

        var lines = StatusLayout.Render(view with { Name = string.Empty }, StatusCases.Now).Split('\n');

        Assert.Equal("  name      " + view.Address.Value, lines[1]);
    }
}
