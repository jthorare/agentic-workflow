namespace Aw.Sindri.Core.Graph.Failures;

/// <summary>How a failure sentence names a step: its own title, else its block kind in words ("run_agent" reads "Run agent"); never its node id (#3304).</summary>
public static class StepTitle
{
    /// <summary>The step's name in a sentence.</summary>
    /// <param name="node">The node.</param>
    /// <returns>Its title, or its kind in words.</returns>
    public static string Of(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Title is { Length: > 0 } own)
        {
            return own;
        }

        var words = node.Type.Replace('_', ' ');
        return words.Length == 0 ? words : words[..1].ToUpperInvariant() + words[1..];
    }
}
