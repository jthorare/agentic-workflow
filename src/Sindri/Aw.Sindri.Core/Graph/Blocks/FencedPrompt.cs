using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Interpolation;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Renders a node's prompt text with every reference fenced as untrusted; the one shape the 'prompt' block and 'run_agent' both use.</summary>
public static class FencedPrompt
{
    private static readonly JsonSerializerOptions _compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Replaces each '{{ref}}' of <paramref name="raw"/> with its value inside the untrusted fence; the author's own words stay bare.</summary>
    /// <param name="raw">The prompt as written.</param>
    /// <param name="resolve">Reads a reference; null for a missing value.</param>
    /// <param name="untrusted">Fences a value: the source ("workflow:trigger", "workflow:item", "workflow:run" or "workflow:node:&lt;nodeId&gt;") and the text.</param>
    /// <returns>The prompt text.</returns>
    public static string Render(string raw, Func<string, JsonNode?> resolve, Func<string, string, string> untrusted)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(resolve);
        ArgumentNullException.ThrowIfNull(untrusted);
        var rendered = Template.Render(raw, reference =>
        {
            var node = resolve(reference);
            if (node is null)
            {
                return node;
            }

            // why: a trigger payload and every node output (an http body, search facts) carry text a stranger can write; only the author's own prompt words stay bare
            var text = node is JsonValue value && value.TryGetValue<string>(out var s) ? s : node.ToJsonString(_compact);
            return JsonValue.Create(untrusted(SourceOf(reference), text));
        });
        return rendered switch
        {
            null => string.Empty,
            JsonValue value when value.TryGetValue<string>(out var s) => s,
            _ => rendered.ToJsonString(_compact),
        };
    }

    /// <summary>
    /// The fence source of a reference: "workflow:trigger" for the trigger, "workflow:item" for a for_each item, "workflow:run" for the run's own
    /// values ('itemIndex', 'workflow.*'), "workflow:node:&lt;nodeId&gt;" for a node output. The roots are GraphValidator's scope roots.
    /// </summary>
    private static string SourceOf(string reference)
    {
        // why: 'item' is often stranger text (a for_each over an http list); 'itemIndex' and 'workflow.*' are Sindri's own values, still fenced, under the run's source, so no root reads as a node that doesn't exist (issue 2446)
        var head = reference.Split('.', 2)[0];
        return head switch
        {
            "trigger" => "workflow:trigger",
            "item" => "workflow:item",
            "itemIndex" or "workflow" => "workflow:run",
            _ => "workflow:node:" + head,
        };
    }
}
