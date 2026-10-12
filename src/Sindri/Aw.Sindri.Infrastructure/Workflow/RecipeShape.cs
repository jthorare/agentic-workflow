using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// Checks the normalized tree against the long-form records' JSON metadata before binding, so every unknown or missing
/// field (RCP-SCHEMA-001) and every value of the wrong type (RCP-SCHEMA-002; a malformed duration is RCP-DUR-001) is
/// reported at its own node, not just the first one binding would throw on. It fills what the long form leaves out:
/// an absent list or map is empty and an absent optional field is null, so both forms bind to the same record.
/// </summary>
internal sealed class RecipeShape(JsonSerializerOptions options, ReaderFindings findings)
{
    /// <summary>Collections the recipe must write, even though the long form could hold them empty (spec §3.2).</summary>
    private static readonly HashSet<(Type, string)> _requiredCollections = [(typeof(Recipe), "states"), (typeof(Recipe), "transitions")];

    public void Check(JsonNode? node, Type type, string path, bool nullable)
    {
        if (findings.IsRefused(path))
        {
            return;
        }

        if (node is null)
        {
            Empty(type, path, nullable || Nullable.GetUnderlyingType(type) is not null);
            return;
        }

        var info = options.GetTypeInfo(type);   // a nullable value type is a leaf, read as itself
        switch (info.Kind)
        {
            case JsonTypeInfoKind.Object:
                Object(node, info, path);
                break;
            case JsonTypeInfoKind.Enumerable:
                List(node, info, path);
                break;
            case JsonTypeInfoKind.Dictionary:
                Map(node, info, path);
                break;
            case JsonTypeInfoKind.None:
            default:
                Leaf(node, info, path);
                break;
        }
    }

    private void Empty(Type type, string path, bool nullable)
    {
        if (!nullable)
        {
            findings.Add("RCP-SCHEMA-002", path, $"'{Last(path)}' has no value", $"write {RecipeTypes.Expected(type)}");
            findings.Refuse(path);
        }
    }

    private void Object(JsonNode node, JsonTypeInfo info, string path)
    {
        if (node is not JsonObject map)
        {
            findings.Add("RCP-SCHEMA-002", path, $"{RecipeTypes.What(info.Type)} is written as {Shape(node)}", $"write {RecipeTypes.What(info.Type)} as a mapping");
            findings.Refuse(path);
            return;
        }

        var fields = info.Properties.ToDictionary(static p => p.Name, StringComparer.Ordinal);
        foreach (var key in map.Select(static p => p.Key).ToList())
        {
            var at = ReaderFindings.Join(path, key);
            if (fields.TryGetValue(key, out var field))
            {
                Check(map[key], field.PropertyType, at, IsNullable(field));
                continue;
            }

            findings.Add("RCP-SCHEMA-001", at, $"'{key}' isn't a field of {RecipeTypes.What(info.Type)}",
                $"remove it; {RecipeTypes.What(info.Type)} has {string.Join(", ", fields.Keys)}");
            findings.RefuseUnknown(at, map, key);
        }

        foreach (var field in fields.Values.Where(f => !map.ContainsKey(f.Name)))
        {
            Fill(map, info.Type, field, path);
        }
    }

    private void Fill(JsonObject map, Type owner, JsonPropertyInfo field, string path)
    {
        var kind = options.GetTypeInfo(field.PropertyType).Kind;
        if (IsNullable(field))
        {
            map[field.Name] = null;
        }
        else if (kind is JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary && !_requiredCollections.Contains((owner, field.Name)))
        {
            map[field.Name] = kind == JsonTypeInfoKind.Enumerable ? new JsonArray() : new JsonObject();
        }
        else
        {
            findings.Add("RCP-SCHEMA-001", ReaderFindings.Join(path, field.Name), $"{RecipeTypes.What(owner)} has no '{field.Name}'",
                $"add '{field.Name}': {RecipeTypes.Expected(field.PropertyType)}");
        }
    }

    private void List(JsonNode node, JsonTypeInfo info, string path)
    {
        if (node is not JsonArray list)
        {
            findings.Add("RCP-SCHEMA-002", path, $"'{Last(path)}' is written as {Shape(node)}", "write it as a list");
            findings.Refuse(path);
            return;
        }

        for (var i = 0; i < list.Count; i++)
        {
            Check(list[i], info.ElementType!, string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]"), nullable: false);   // why: an Enumerable type info always has an element type
        }
    }

    private void Map(JsonNode node, JsonTypeInfo info, string path)
    {
        if (node is not JsonObject map)
        {
            findings.Add("RCP-SCHEMA-002", path, $"'{Last(path)}' is written as {Shape(node)}", "write it as a mapping");
            findings.Refuse(path);
            return;
        }

        foreach (var (key, value) in map)
        {
            var at = ReaderFindings.Join(path, key);
            var keyType = info.KeyType!;   // why: a Dictionary type info always has a key type
            if (keyType != typeof(string) && !Reads(JsonValue.Create(key), options.GetTypeInfo(keyType)))
            {
                findings.Add("RCP-SCHEMA-002", at, $"key '{key}' isn't {RecipeTypes.Expected(keyType)}", $"write {RecipeTypes.Expected(keyType)}");
                findings.Refuse(at);
                continue;
            }

            Check(value, info.ElementType!, at, nullable: false);   // why: a Dictionary type info always has an element type
        }
    }

    private void Leaf(JsonNode node, JsonTypeInfo info, string path)
    {
        if (Reads(node, info))
        {
            return;
        }

        var type = Nullable.GetUnderlyingType(info.Type) ?? info.Type;
        var expected = RecipeTypes.Expected(type);
        var rule = type == typeof(RecipeDuration) ? "RCP-DUR-001" : "RCP-SCHEMA-002";
        var fix = type == typeof(RecipeDuration) ? "write a whole number and m, h or d, at least 1m ('10m', '4h', '2d')" : $"write {expected}";
        findings.Add(rule, path, $"{Written(node)} isn't {expected}", fix);
        findings.Refuse(path);
    }

    /// <summary>
    /// Whether <paramref name="node"/> binds as <paramref name="info"/>'s type. An enum takes exactly one of its names:
    /// the converter also accepts numbers, numeric text, any case and comma lists, so the value must write back as the
    /// same text.
    /// </summary>
    private static bool Reads(JsonNode node, JsonTypeInfo info)
    {
        try
        {
            var value = JsonSerializer.Deserialize(node, info);
            var type = Nullable.GetUnderlyingType(info.Type) ?? info.Type;
            return !type.IsEnum
                || (node is JsonValue text && text.GetValueKind() == JsonValueKind.String
                    && string.Equals(JsonSerializer.Serialize(value, info), node.ToJsonString(), StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsNullable(JsonPropertyInfo field) =>
        field.AssociatedParameter?.IsNullable ?? field.IsSetNullable;

    private static string Last(string path)
    {
        var cut = path.LastIndexOf('.');
        return cut < 0 ? path : path[(cut + 1)..];
    }

    private static string Shape(JsonNode node) => node switch
    {
        JsonObject => "a mapping",
        JsonArray => "a list",
        _ => Written(node),
    };

    private static string Written(JsonNode node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String
        ? $"'{value.GetValue<string>()}'"
        : node.ToJsonString(options: null);
}
