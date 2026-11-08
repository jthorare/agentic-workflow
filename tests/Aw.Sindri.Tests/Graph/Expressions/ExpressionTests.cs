using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Expressions;

namespace Aw.Sindri.Tests.Graph.Expressions;

/// <summary>The condition language: precedence, exists(), truthiness, typed compares, parse errors with a column, and the reference list.</summary>
[Lane(TestLane.Unit)]
public sealed class ExpressionTests
{
    private static readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal)
    {
        ["a"] = "true",
        ["b"] = "false",
        ["c"] = "true",
        ["nul"] = "null",
        ["empty"] = "\"\"",
        ["zero"] = "0",
        ["list"] = "[]",
        ["full"] = "[1]",
        ["text"] = "\"hello\"",
        ["n.count"] = "5",
        ["n.name"] = "\"5\"",
        ["trigger.pr"] = "12",
        ["item"] = "\"x\"",
    };

    private static bool Run(string text)
    {
        Assert.True(ConditionExpression.TryParse(text, out var expression, out var error), error);
        Assert.NotNull(expression);
        return expression.Evaluate(Resolve);
    }

    private static JsonNode? Resolve(string reference) =>
        _values.TryGetValue(reference, out var json) && json is not null ? JsonNode.Parse(json) : null;

    [Theory]
    [InlineData("not a and b or c", true)]
    [InlineData("not (a and b) or c", true)]
    [InlineData("not a and (b or c)", false)]
    [InlineData("a or b and b", true)]
    [InlineData("(a or b) and b", false)]
    [InlineData("not not a", true)]
    [InlineData("a and not b", true)]
    [InlineData("not n.count == 5", false)]
    public void Not_binds_tighter_than_comparison_then_and_then_or(string text, bool expected) =>
        Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("exists(a)", true)]
    [InlineData("exists(b)", true)]
    [InlineData("exists(nul)", false)]
    [InlineData("exists(missing.field)", false)]
    [InlineData("exists({{trigger.pr}})", true)]
    [InlineData("not exists(missing)", true)]
    [InlineData("exists(missing) or a", true)]
    public void Exists_is_true_when_the_reference_resolves_to_a_non_null_value(string text, bool expected) =>
        Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("nul", false)]
    [InlineData("empty", false)]
    [InlineData("zero", false)]
    [InlineData("b", false)]
    [InlineData("list", false)]
    [InlineData("missing", false)]
    [InlineData("a", true)]
    [InlineData("text", true)]
    [InlineData("full", true)]
    [InlineData("trigger.pr", true)]
    [InlineData("{{item}}", true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", false)]
    [InlineData("\"\"", false)]
    [InlineData("\"x\"", true)]
    [InlineData("0", false)]
    [InlineData("3", true)]
    public void Null_false_zero_empty_string_and_empty_array_are_falsy(string text, bool expected) =>
        Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("n.count == 5", true)]
    [InlineData("n.count == 5.0", true)]
    [InlineData("n.count != 6", true)]
    [InlineData("n.count > 4", true)]
    [InlineData("n.count >= 5", true)]
    [InlineData("n.count < 5", false)]
    [InlineData("n.count <= 5", true)]
    [InlineData("n.count < 10", true)]
    [InlineData("n.name == \"5\"", true)]
    [InlineData("text == \"hello\"", true)]
    [InlineData("text < \"world\"", true)]
    [InlineData("nul == null", true)]
    [InlineData("missing == null", true)]
    [InlineData("a == true", true)]
    public void Numbers_compare_as_numbers_and_strings_as_strings(string text, bool expected) =>
        Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("n.count == \"5\"")]
    [InlineData("n.name == 5")]
    [InlineData("n.name < 10")]
    [InlineData("n.count > \"4\"")]
    [InlineData("a == 1")]
    [InlineData("text >= null")]
    public void Comparing_different_types_is_false_and_never_throws(string text) =>
        Assert.False(Run(text));

    [Fact]
    public void Not_equal_across_types_is_true_because_the_values_are_not_equal() =>
        Assert.True(Run("n.count != \"5\""));

    [Theory]
    [InlineData("a and", 6)]
    [InlineData("(a or b", 8)]
    [InlineData("a b", 3)]
    [InlineData("a == ", 6)]
    [InlineData("", 1)]
    [InlineData("exists()", 8)]
    [InlineData("exists(a", 9)]
    [InlineData("len(a)", 4)]
    [InlineData("a = 1", 3)]
    [InlineData("\"open", 1)]
    [InlineData("a and )", 7)]
    [InlineData("{{a", 1)]
    [InlineData("a $ b", 3)]
    public void A_parse_error_names_the_column_and_says_the_fix(string text, int column)
    {
        Assert.False(ConditionExpression.TryParse(text, out var expression, out var error));

        Assert.Null(expression);
        Assert.NotNull(error);
        Assert.Contains("column " + column.ToString(CultureInfo.InvariantCulture), error, StringComparison.Ordinal);
    }

    [Fact]
    public void Calling_a_function_other_than_exists_says_only_exists_is_available()
    {
        Assert.False(ConditionExpression.TryParse("len(a) > 1", out _, out var error));

        Assert.Contains("exists", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a and b or a", new[] { "a", "b" })]
    [InlineData("n.count > 1 and exists(trigger.pr) or {{item}} == \"x\"", new[] { "n.count", "trigger.pr", "item" })]
    [InlineData("n.count == 1 or n.count == 2", new[] { "n.count" })]
    [InlineData("\"a\" == 1", new string[0])]
    [InlineData("true and not null", new string[0])]
    public void References_lists_each_reference_once_in_order_of_first_use(string text, string[] expected)
    {
        Assert.True(ConditionExpression.TryParse(text, out var expression, out var error), error);

        Assert.Equal(expected, expression!.References);
    }

    [Fact]
    public void The_resolver_is_asked_for_the_reference_name_without_braces()
    {
        Assert.True(ConditionExpression.TryParse("{{trigger.pr}} == 12", out var expression, out _));
        var asked = new List<string>();

        var result = expression!.Evaluate(r =>
        {
            asked.Add(r);
            return JsonValue.Create(12);
        });

        Assert.True(result);
        Assert.Equal(["trigger.pr"], asked);
    }
}
