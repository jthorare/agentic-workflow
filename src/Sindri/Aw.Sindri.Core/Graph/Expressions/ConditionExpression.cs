using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Expressions;

/// <summary>
/// A parsed condition.when expression: references (node.field, trigger.x, item, workflow.x, optionally
/// wrapped as {{node.field}}), literals, the six comparisons (equal, not equal, less, at most, greater, at least), and or not, parentheses and exists(ref).
/// Comparing values of different types is false (never an exception), and so is any ordering involving null.
/// </summary>
public sealed class ConditionExpression
{
    private readonly Evaluator.Node _root;

    private ConditionExpression(Evaluator.Node root, IReadOnlyList<string> references)
    {
        _root = root;
        References = references;
    }

    /// <summary>Each reference the expression reads, once, in order of first use.</summary>
    public IReadOnlyList<string> References { get; }

    /// <summary>Parses an expression.</summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expression">The parsed expression, or null when the text does not parse.</param>
    /// <param name="error">The column and the fix, or null when it parses.</param>
    /// <returns>True when the text parses.</returns>
    public static bool TryParse(string text, out ConditionExpression? expression, out string? error)
    {
        expression = null;
        if (!Parser.TryParse(text, out var root, out var references, out error))
        {
            return false;
        }

        expression = new ConditionExpression(root, references);
        return true;
    }

    /// <summary>Evaluates the expression.</summary>
    /// <param name="resolve">Reads a reference's value; null for a missing one.</param>
    /// <returns>The truthiness of the result.</returns>
    public bool Evaluate(Func<string, JsonNode?> resolve) => Evaluator.IsTrue(_root, resolve);
}
