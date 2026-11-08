using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>A block's config schema: the fields it takes. Anything else in the config is a finding.</summary>
/// <param name="Fields">The fields.</param>
public sealed record BlockSchema(IReadOnlyList<BlockField> Fields)
{
    /// <summary>Checks <paramref name="config"/> against the fields.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>One problem per field: the field's name and what to change; empty when the config fits.</returns>
    public IReadOnlyList<(string Field, string Problem)> Check(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var problems = new List<(string Field, string Problem)>();
        foreach (var field in Fields)
        {
            if (!config.TryGetPropertyValue(field.Name, out var value) || value is null)
            {
                if (field.Required)
                {
                    problems.Add((field.Name, $"is required; add it as {Describe(field.Type)}"));
                }

                continue;
            }

            if (!Fits(value, field.Type))
            {
                problems.Add((field.Name, $"must be {Describe(field.Type)}; change it"));
            }
        }

        foreach (var (name, _) in config)
        {
            if (!Fields.Any(f => string.Equals(f.Name, name, StringComparison.Ordinal)))
            {
                problems.Add((name, "is not a field of this block; remove it"));
            }
        }

        return problems;
    }

    private static bool Fits(JsonNode value, BlockFieldType type) => type switch
    {
        BlockFieldType.Text => value.GetValueKind() == JsonValueKind.String,
        BlockFieldType.Boolean => value.GetValueKind() is JsonValueKind.True or JsonValueKind.False || Reference(value),
        BlockFieldType.Number => value.GetValueKind() == JsonValueKind.Number || Reference(value),
        BlockFieldType.TextList => value is JsonArray list && list.All(static item => item?.GetValueKind() == JsonValueKind.String),
        BlockFieldType.Map => value is JsonObject,
        _ => false,
    };

    // why: a boolean or number field may be one reference, which resolves to that type at run time
    private static bool Reference(JsonNode value) =>
        value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Contains("{{", StringComparison.Ordinal);

    private static string Describe(BlockFieldType type) => type switch
    {
        BlockFieldType.Text => "text",
        BlockFieldType.Boolean => "true or false",
        BlockFieldType.Number => "a number",
        BlockFieldType.TextList => "a list of text",
        BlockFieldType.Map => "an object",
        _ => "a value",
    };
}
