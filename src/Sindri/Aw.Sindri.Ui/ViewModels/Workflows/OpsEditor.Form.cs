using System.Text.Json;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The workflow editor's logic (<see cref="OpsEditor"/>, continued): the form's parts: the nodes read from a spec, the Code step's Advanced group and the example under its code box (#3682).</summary>
internal sealed partial class OpsEditor
{
    /// <summary>True when the selected step has settings in an Advanced group.</summary>
    public bool HasAdvancedFields => Fields.Any(f => f.InAdvanced);

    /// <summary>True when the Advanced group is open.</summary>
    public bool AdvancedOpen
    {
        get;
        set
        {
            Set(ref field, value);
            foreach (var f in Fields)
            {
                f.GroupOpen = value;
            }
        }
    }

    /// <summary>The Advanced toggle's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string AdvancedText => Labels.WfFieldAdvanced;
#pragma warning restore CA1822, MA0041

    private static string ExampleOf(WorkflowBlock block, string field) =>
        block.ExampleConfig is { ValueKind: JsonValueKind.Object } example && example.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && string.Equals(field, "code", StringComparison.Ordinal)
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static List<EditorNode> ReadNodes(JsonElement spec)
    {
        var nodes = new List<EditorNode>();
        if (spec.ValueKind != JsonValueKind.Object || !spec.TryGetProperty("nodes", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return nodes;
        }

        foreach (var node in list.EnumerateArray())
        {
            var title = node.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            var config = node.TryGetProperty("config", out var c) ? c.Clone() : default;
            nodes.Add(new EditorNode(node.GetProperty("id").GetString() ?? string.Empty, node.GetProperty("type").GetString() ?? string.Empty, title, config));
        }

        return nodes;
    }
}
