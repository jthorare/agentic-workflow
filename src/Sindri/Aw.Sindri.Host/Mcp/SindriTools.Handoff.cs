using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

internal static partial class SindriTools
{
    /// <summary>sindri_handoff's name.</summary>
    internal const string Handoff = "sindri_handoff";

    /// <summary>sindri_handoff's definition.</summary>
    internal static Tool HandoffTool { get; } = new()
    {
        Name = Handoff,
        Description = "Hand work to Sindri on this machine: makes a work item and keeps the whole prompt, branch, links and labels in it. "
            + "The prompt may be 65,536 characters; the item body keeps its first 4,000. Same as 'sindri handoff send'.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["title"] = ContextTools.Property("string", "The item's title (1 to 200 characters)", bounds: new ContextTools.PropertyBounds(MinLength: 1, MaxLength: 200)),
            ["source"] = ContextTools.Property("string", "Who sends it: 1 to 32 characters of a-z, 0-9, '.', '_' and '-' (default mcp)"),
            ["repo"] = ContextTools.Property("string", "A repo of the profile; left out, the repo of this registration"),
            ["branch"] = ContextTools.Property("string", "The branch it names"),
            ["prompt"] = ContextTools.Property("string", "The full prompt", bounds: new ContextTools.PropertyBounds(MaxLength: 65_536)),
            ["offer"] = ContextTools.Property("string", "Your id for this offer; left out, the item id stands in"),
            ["links"] = new JsonObject { ["type"] = "array", ["description"] = "Links, at most 50", ["items"] = new JsonObject { ["type"] = "string" } },
            ["labels"] = new JsonObject { ["type"] = "array", ["description"] = "Labels, at most 20", ["items"] = new JsonObject { ["type"] = "string" } },
        }, required: ["title"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = false, OpenWorldHint = false },
    };
}
