using System.Text.Json.Nodes;

namespace Aw.Sindri.Toolkit.Tests;

public sealed class ToolkitSchemaTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_input_schema_comes_from_the_request_record_and_marks_required_fields()
    {
        var schema = SampleToolkit.Create().Tools.Single(t => t.Name == "diff_contracts").InputSchema;

        Assert.Equal<string>(["left", "right"], Required(schema), StringComparer.Ordinal);
        var properties = schema["properties"]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal<string>(["left", "limit", "note", "right"], properties, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_nullable_member_is_optional_and_accepts_null()
    {
        var schema = SampleToolkit.Create().Tools.Single(t => t.Name == "diff_contracts").InputSchema;

        Assert.DoesNotContain("note", Required(schema), StringComparer.Ordinal);
        Assert.DoesNotContain("limit", Required(schema), StringComparer.Ordinal);
        Assert.Contains("null", schema["properties"]!["note"]!["type"]!.AsArray().Select(n => n!.GetValue<string>()), StringComparer.Ordinal);
    }

    private static string[] Required(JsonObject schema) =>
        [.. schema["required"]!.AsArray().Select(n => n!.GetValue<string>()).Order(StringComparer.Ordinal)];
}
