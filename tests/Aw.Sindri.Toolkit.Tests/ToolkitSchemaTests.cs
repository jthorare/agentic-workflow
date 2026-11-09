using System.Text.Json.Nodes;

namespace Aw.Sindri.Toolkit.Tests;

public sealed class ToolkitSchemaTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_input_schema_comes_from_the_request_record_and_marks_required_fields()
    {
        var schema = SampleToolkit.Create().Tools.Single(t => t.Name == "diff_contracts").InputSchema;

        Assert.Equal(["left", "right"], Required(schema), StringComparer.Ordinal);
        var properties = schema["properties"]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["left", "limit", "note", "right"], properties, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_nullable_member_is_optional_and_accepts_null()
    {
        var schema = SampleToolkit.Create().Tools.Single(t => t.Name == "diff_contracts").InputSchema;

        Assert.DoesNotContain("note", Required(schema), StringComparer.Ordinal);
        Assert.DoesNotContain("limit", Required(schema), StringComparer.Ordinal);
        Assert.Contains("null", schema["properties"]!["note"]!["type"]!.AsArray().Select(n => n!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tool_whose_response_is_not_an_object_is_refused_with_the_declaration_code()
    {
        var toolkit = Toolkit.Named("bare").UseJson(StringResponseJson.Default);

        var failure = Assert.Throws<ToolkitException>(() =>
            toolkit.Tool<DiffRequest, string>("t", "d", (_, _, _) => Task.FromResult("x")));

        Assert.Equal(ToolkitErrors.InvalidDeclaration, failure.Definition);
        Assert.Contains("object schema", failure.Message, StringComparison.Ordinal);
    }

    private static string[] Required(JsonObject schema) =>
        [.. schema["required"]!.AsArray().Select(n => n!.GetValue<string>()).Order(StringComparer.Ordinal)];
}
