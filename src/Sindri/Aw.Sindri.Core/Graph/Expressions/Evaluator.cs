using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Expressions;

/// <summary>The parsed tree and its evaluation. Values are JSON nodes; a result is reduced to truthiness at the end.</summary>
internal static class Evaluator
{
    internal abstract record Node;

    internal sealed record Literal(JsonNode? Value) : Node;

    internal sealed record Reference(string Name) : Node;

    internal sealed record Exists(string Name) : Node;

    internal sealed record Not(Node Operand) : Node;

    internal sealed record And(Node Left, Node Right) : Node;

    internal sealed record Or(Node Left, Node Right) : Node;

    internal sealed record Compare(Tokenizer.Kind Op, Node Left, Node Right) : Node;

    internal static bool IsTrue(Node root, Func<string, JsonNode?> resolve) => Truthy(Eval(root, resolve));

    private static JsonNode? Eval(Node node, Func<string, JsonNode?> resolve) => node switch
    {
        Literal l => l.Value,
        Reference r => resolve(r.Name),
        Exists e => JsonValue.Create(resolve(e.Name) is not null),
        Not n => JsonValue.Create(!Truthy(Eval(n.Operand, resolve))),
        And a => JsonValue.Create(Truthy(Eval(a.Left, resolve)) && Truthy(Eval(a.Right, resolve))),
        Or o => JsonValue.Create(Truthy(Eval(o.Left, resolve)) || Truthy(Eval(o.Right, resolve))),
        Compare c => JsonValue.Create(Compared(c.Op, Eval(c.Left, resolve), Eval(c.Right, resolve))),
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    private static bool Truthy(JsonNode? value)
    {
        var v = Classify(value);
        return v.Kind switch
        {
            ValueKind.Null => false,
            ValueKind.Bool => v.Flag,
            ValueKind.Number => v.Number != 0,
            ValueKind.Text => v.Text.Length > 0,
            ValueKind.Composite => value is not JsonArray array || array.Count > 0,
            _ => false,
        };
    }

    private static Value Classify(JsonNode? node)
    {
        if (node is null)
        {
            return new Value(ValueKind.Null);
        }

        var kind = node.GetValueKind();
        if (kind == JsonValueKind.Null)
        {
            return new Value(ValueKind.Null);
        }

        if (kind is JsonValueKind.True or JsonValueKind.False)
        {
            return new Value(ValueKind.Bool, Flag: kind == JsonValueKind.True, Raw: node);
        }

        if (kind == JsonValueKind.Number)
        {
            var parsed = double.TryParse(node.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : double.NaN;
            return new Value(ValueKind.Number, Number: parsed, Raw: node);
        }

        if (kind == JsonValueKind.String)
        {
            return new Value(ValueKind.Text, Text: node.GetValue<string>(), Raw: node);
        }

        return new Value(ValueKind.Composite, Raw: node);
    }

    // why: different types are never equal and never ordered, so only != holds
    private static bool Compared(Tokenizer.Kind op, JsonNode? left, JsonNode? right)
    {
        var a = Classify(left);
        var b = Classify(right);
        if (a.Kind != b.Kind)
        {
            return op == Tokenizer.Kind.Ne;
        }

        if (a.Kind == ValueKind.Number)
        {
            return Apply(op, a.Number.CompareTo(b.Number), unordered: double.IsNaN(a.Number) || double.IsNaN(b.Number));
        }

        if (a.Kind == ValueKind.Text)
        {
            return Apply(op, string.CompareOrdinal(a.Text, b.Text), unordered: false);
        }

        if (a.Kind == ValueKind.Null)
        {
            return op == Tokenizer.Kind.Eq;
        }

        var equal = a.Kind == ValueKind.Bool ? a.Flag == b.Flag : JsonNode.DeepEquals(a.Raw, b.Raw);
        if (op == Tokenizer.Kind.Eq)
        {
            return equal;
        }

        return op == Tokenizer.Kind.Ne && !equal;
    }

    private static bool Apply(Tokenizer.Kind op, int order, bool unordered)
    {
        if (op == Tokenizer.Kind.Ne)
        {
            return unordered || order != 0;
        }

        if (unordered)
        {
            return false;
        }

        if (op == Tokenizer.Kind.Eq)
        {
            return order == 0;
        }

        if (op == Tokenizer.Kind.Lt)
        {
            return order < 0;
        }

        if (op == Tokenizer.Kind.Le)
        {
            return order <= 0;
        }

        return op == Tokenizer.Kind.Gt ? order > 0 : op == Tokenizer.Kind.Ge && order >= 0;
    }

    private enum ValueKind
    {
        Null,
        Bool,
        Number,
        Text,
        Composite,
    }

    private readonly record struct Value(ValueKind Kind, bool Flag = false, double Number = 0, string Text = "", JsonNode? Raw = null);
}
