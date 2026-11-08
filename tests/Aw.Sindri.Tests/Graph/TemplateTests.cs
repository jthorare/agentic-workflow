using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Interpolation;

namespace Aw.Sindri.Tests.Graph;

[Lane(TestLane.Unit)]
public sealed class TemplateTests
{
    private static JsonNode? Lookup(string reference) => reference switch
    {
        "agent.drifted" => JsonValue.Create(value: true),
        "agent.prUrl" => JsonValue.Create("https://example.test/pr/7"),
        "agent.usage" => new JsonObject { ["tokens"] = 12, ["models"] = new JsonArray("a", "b") },
        _ => null,
    };

    [Fact]
    public void A_string_that_is_exactly_one_reference_keeps_the_type_and_a_mixed_string_renders_compact_JSON()
    {
        var whole = Template.Render("{{agent.drifted}}", Lookup);
        var padded = Template.Render(" {{ agent.drifted }} ", Lookup);
        var mixed = Template.Render("PR {{agent.prUrl}} used {{agent.usage}} then {{agent.missing}}.", Lookup);

        Assert.Equal(JsonValueKind.True, whole!.GetValueKind());
        Assert.Equal(JsonValueKind.String, padded!.GetValueKind());
        Assert.Equal(" true ", padded.GetValue<string>());
        Assert.Equal("PR https://example.test/pr/7 used {\"tokens\":12,\"models\":[\"a\",\"b\"]} then .", mixed!.GetValue<string>());
        Assert.Null(Template.Render("{{agent.missing}}", Lookup));
    }

    [Fact]
    public void References_lists_each_reference_in_order_trimmed()
    {
        Assert.Equal(["recall.facts", "trigger.at", "item"], Template.References("Context {{recall.facts}} at {{ trigger.at }} for {{item}}"));
        Assert.Empty(Template.References("no references here"));
    }
}
