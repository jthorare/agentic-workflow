using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>
/// The 'usage' a node run keeps (#3805): '{"tokens":N}', the input and output tokens of a prompt node's model calls or a run_agent node's session.
/// A run's total, the number its token budget caps and its run page shows, is the sum over its node runs.
/// </summary>
public static class NodeRunUsage
{
    /// <summary>The usage JSON for <paramref name="tokens"/>.</summary>
    /// <param name="tokens">The tokens the node used.</param>
    /// <returns>The JSON, or null when the node used none.</returns>
    public static string? Of(long tokens) => tokens > 0 ? "{\"tokens\":" + tokens.ToString(CultureInfo.InvariantCulture) + "}" : null;

    /// <summary>The tokens a stored usage names.</summary>
    /// <param name="usage">The stored usage, or null.</param>
    /// <returns>The count; 0 for null, for text of another shape and for a count below 1.</returns>
    public static long Tokens(string? usage)
    {
        if (usage is null)
        {
            return 0;
        }

        try
        {
            return JsonNode.Parse(usage)?["tokens"] is JsonValue value && value.TryGetValue<long>(out var tokens) && tokens > 0 ? tokens : 0;
        }
        catch (JsonException)   // boundary: a usage cut by the store, or written by hand, is not a count
        {
            return 0;
        }
    }
}
