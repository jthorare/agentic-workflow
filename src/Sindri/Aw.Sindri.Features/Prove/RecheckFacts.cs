using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Prove;

/// <summary>What the re-proof child's Steps share: reading a 'with:' argument, the head of the PR's snapshot, and the two files of the instance's run area that carry the plan and the rerun between Steps.</summary>
internal static class RecheckFacts
{
    /// <summary>The run-area file the plan Step writes: planned, headSha, workspace, scripts.</summary>
    internal const string PlanFile = "recheck/plan.json";

    /// <summary>The run-area file the rerun Step writes: ran, results[{script, passed, detail}].</summary>
    internal const string ScriptsFile = "recheck/scripts.json";

    /// <summary>The JSON a 'with:' argument holds (the stored text of a fact, or a literal); null when absent.</summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value.</returns>
    internal static JsonNode? Argument(StepContext context, string name)
    {
        if (!context.Arguments.TryGetValue(name, out var element))
        {
            return null;
        }

        var text = element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText();
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);   // a bare path the recipe passed as plain text
        }
    }

    /// <summary>The text a node holds, or null.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The string.</returns>
    internal static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>The head sha of a PR snapshot ('head.sha'), or of a plain object with 'head' text.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The sha.</returns>
    internal static string? HeadSha(JsonNode? snapshot) => Text(snapshot?["head"]?["sha"]) ?? Text(snapshot?["head"]);

    /// <summary>Whether a script path stays inside the worktree: relative, and with no climbing segment.</summary>
    /// <param name="script">The path.</param>
    /// <returns>True when it is safe to run there.</returns>
    internal static bool Inside(string script) => script.Length > 0 && !Path.IsPathRooted(script) && !script.Split('/', '\\').Contains("..", StringComparer.Ordinal);
}
