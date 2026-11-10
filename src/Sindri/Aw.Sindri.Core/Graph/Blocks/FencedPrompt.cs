using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Renders a node's prompt text with every reference fenced as untrusted; the one shape the 'prompt' block and 'run_agent' both use.</summary>
public static class FencedPrompt
{
    /// <summary>Replaces each '{{ref}}' of <paramref name="raw"/> with its value inside the untrusted fence; the author's own words stay bare.</summary>
    /// <param name="raw">The prompt as written.</param>
    /// <param name="resolve">Reads a reference; null for a missing value.</param>
    /// <param name="untrusted">Fences a value: the source ("workflow:trigger" or "workflow:node:&lt;nodeId&gt;") and the text.</param>
    /// <returns>The prompt text.</returns>
    public static string Render(string raw, Func<string, JsonNode?> resolve, Func<string, string, string> untrusted) =>
        throw new NotSupportedException();
}
