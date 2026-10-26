using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Scoping;

/// <summary>
/// The JSON Schema subset the scoping schemas use (schemas/scoping, draft 2020-12): type, const, enum, the string, number, array and
/// object keywords, $ref into $defs, allOf, anyOf, oneOf, not and if/then/else, contains with minContains and maxContains. It reports every
/// place a value breaks the schema; it is not a general validator, and a keyword it does not know is ignored.
/// </summary>
public sealed class JsonSchemaLite(JsonNode root)
{
    private static readonly TimeSpan _patternTimeout = TimeSpan.FromSeconds(1);

    private readonly Dictionary<string, Regex> _patterns = new(StringComparer.Ordinal);

    /// <summary>Checks <paramref name="instance"/> against <paramref name="schema"/> (a node of this validator's document).</summary>
    public IReadOnlyList<(string Path, string Message)> Validate(JsonNode? instance, JsonNode? schema)
    {
        var errors = new List<SchemaError>();
        Check(instance, schema, string.Empty, errors);
        return [.. errors.Select(static e => (e.Path, e.Message))];
    }

    private void Check(JsonNode? value, JsonNode? schema, string path, List<SchemaError> errors)
    {
        if (schema is not JsonObject rules)
        {
            return;
        }

        if (Str(rules, "$ref") is { } reference)
        {
            Check(value, Resolve(reference), path, errors);
            return;
        }

        if (rules["type"] is { } type && !TypeMatches(value, type))
        {
            errors.Add(new SchemaError(path, $"expected {Describe(type)}, found {Kind(value)}"));
            return;
        }

        CheckExact(value, rules, path, errors);
        CheckString(value, rules, path, errors);
        CheckNumber(value, rules, path, errors);
        if (value is JsonObject obj)
        {
            CheckObject(obj, rules, path, errors);
        }

        if (value is JsonArray array)
        {
            CheckArray(array, rules, path, errors);
        }

        CheckCombinators(value, rules, path, errors);
    }

    private static void CheckExact(JsonNode? value, JsonObject rules, string path, List<SchemaError> errors)
    {
        if (rules.ContainsKey("const") && !JsonNode.DeepEquals(rules["const"], value))
        {
            errors.Add(new SchemaError(path, $"must be {(rules["const"] is { } expected ? expected.ToJsonString() : "null")}"));
        }

        if (rules["enum"] is JsonArray allowed && !allowed.Any(a => JsonNode.DeepEquals(a, value)))
        {
            errors.Add(new SchemaError(path, $"not one of {string.Join('|', allowed.Select(static a => a?.ToString() ?? "null"))}"));
        }
    }

    private void CheckString(JsonNode? value, JsonObject rules, string path, List<SchemaError> errors)
    {
        if (value is not JsonValue v || !v.TryGetValue<string>(out var text))
        {
            return;
        }

        if (Number(rules, "minLength") is { } min && text.Length < min)
        {
            errors.Add(new SchemaError(path, $"shorter than {min.ToString(CultureInfo.InvariantCulture)}"));
        }

        if (Number(rules, "maxLength") is { } max && text.Length > max)
        {
            errors.Add(new SchemaError(path, $"longer than {max.ToString(CultureInfo.InvariantCulture)}"));
        }

        if (Str(rules, "pattern") is { } pattern && !Pattern(pattern).IsMatch(text))
        {
            errors.Add(new SchemaError(path, $"'{Clip(text)}' does not match {pattern}"));
        }
    }

    private static void CheckNumber(JsonNode? value, JsonObject rules, string path, List<SchemaError> errors)
    {
        if (value is not JsonValue v || !v.TryGetValue<double>(out var number))
        {
            return;
        }

        if (Number(rules, "minimum") is { } min && number < min)
        {
            errors.Add(new SchemaError(path, $"below {min.ToString(CultureInfo.InvariantCulture)}"));
        }

        if (Number(rules, "maximum") is { } max && number > max)
        {
            errors.Add(new SchemaError(path, $"above {max.ToString(CultureInfo.InvariantCulture)}"));
        }
    }

    private void CheckObject(JsonObject obj, JsonObject rules, string path, List<SchemaError> errors)
    {
        foreach (var name in Required(rules).Where(n => !obj.ContainsKey(n)))
        {
            errors.Add(new SchemaError(path, $"missing '{name}'"));
        }

        var properties = rules["properties"] as JsonObject;
        foreach (var (name, child) in obj)
        {
            if (properties?[name] is { } declared)
            {
                Check(child, declared, $"{path}/{Escape(name)}", errors);
            }
            else if (rules["additionalProperties"] is { } extra)
            {
                if (extra is JsonValue flag && flag.TryGetValue<bool>(out var allowed))
                {
                    if (!allowed)
                    {
                        errors.Add(new SchemaError($"{path}/{Escape(name)}", $"'{name}' is not allowed here"));
                    }
                }
                else
                {
                    Check(child, extra, $"{path}/{Escape(name)}", errors);
                }
            }
        }
    }

    private void CheckArray(JsonArray array, JsonObject rules, string path, List<SchemaError> errors)
    {
        if (Number(rules, "minItems") is { } min && array.Count < min)
        {
            errors.Add(new SchemaError(path, $"fewer than {min.ToString(CultureInfo.InvariantCulture)} items"));
        }

        if (Number(rules, "maxItems") is { } max && array.Count > max)
        {
            errors.Add(new SchemaError(path, $"more than {max.ToString(CultureInfo.InvariantCulture)} items"));
        }

        if (rules["items"] is { } items)
        {
            for (var i = 0; i < array.Count; i++)
            {
                Check(array[i], items, $"{path}/{i.ToString(CultureInfo.InvariantCulture)}", errors);
            }
        }

        if (rules["contains"] is { } contains)
        {
            CheckContains(array, contains, rules, path, errors);
        }
    }

    private void CheckContains(JsonArray array, JsonNode contains, JsonObject rules, string path, List<SchemaError> errors)
    {
        var matching = array.Count(item => Validate(item, contains).Count == 0);
        var least = Number(rules, "minContains") ?? 1;
        if (matching < least)
        {
            errors.Add(new SchemaError(path, $"fewer than {least.ToString(CultureInfo.InvariantCulture)} items match the required shape"));
        }

        if (Number(rules, "maxContains") is { } most && matching > most)
        {
            errors.Add(new SchemaError(path, $"more than {most.ToString(CultureInfo.InvariantCulture)} items match the shape"));
        }
    }

    private void CheckCombinators(JsonNode? value, JsonObject rules, string path, List<SchemaError> errors)
    {
        if (rules["allOf"] is JsonArray all)
        {
            foreach (var part in all)
            {
                Check(value, part, path, errors);
            }
        }

        if (rules["anyOf"] is JsonArray any && !any.Any(part => Validate(value, part).Count == 0))
        {
            errors.Add(new SchemaError(path, "matches none of the allowed shapes"));
        }

        if (rules["oneOf"] is JsonArray one && one.Where(part => Validate(value, part).Count == 0).Take(2).Count() != 1)
        {
            errors.Add(new SchemaError(path, "does not match exactly one of the allowed shapes"));
        }

        CheckNot(value, rules, path, errors);
        if (rules["if"] is { } condition)
        {
            var branch = Validate(value, condition).Count == 0 ? rules["then"] : rules["else"];
            if (branch is not null)
            {
                Check(value, branch, path, errors);
            }
        }
    }

    // 'not': the value must break the subschema, so '"not": {}' forbids a property that is present.
    private void CheckNot(JsonNode? value, JsonObject rules, string path, List<SchemaError> errors)
    {
        if (rules["not"] is { } forbidden && Validate(value, forbidden).Count == 0)
        {
            errors.Add(new SchemaError(path, "is not allowed here"));
        }
    }

    private JsonNode? Resolve(string reference)
    {
        const string Prefix = "#/";
        if (!reference.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var node = (JsonNode?)root;

        foreach (var part in reference[Prefix.Length..].Split('/'))
        {
            node = node is JsonObject obj ? obj[part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)] : null;
        }

        return node;
    }

    private Regex Pattern(string pattern)
    {
        if (!_patterns.TryGetValue(pattern, out var regex))
        {
            regex = new Regex(pattern, RegexOptions.CultureInvariant, _patternTimeout);
            _patterns[pattern] = regex;
        }

        return regex;
    }

    private static bool TypeMatches(JsonNode? value, JsonNode type) =>
        type is JsonArray several ? several.Any(t => t is not null && TypeMatches(value, t)) : TypeIs(value, type.ToString());

    private static bool TypeIs(JsonNode? value, string type) => type switch
    {
        "object" => value is JsonObject,
        "array" => value is JsonArray,
        "string" => value is JsonValue s && s.TryGetValue<string>(out _),
        "boolean" => value is JsonValue b && b.TryGetValue<bool>(out _),
        "null" => value is null,
        "integer" => value is JsonValue i && i.TryGetValue<double>(out var d) && d == Math.Floor(d),
        "number" => value is JsonValue n && n.TryGetValue<double>(out _),
        _ => true,
    };

    private static string Kind(JsonNode? value) => value switch
    {
        null => "null",
        JsonObject => "an object",
        JsonArray => "an array",
        JsonValue v when v.TryGetValue<string>(out _) => "a string",
        JsonValue v when v.TryGetValue<bool>(out _) => "a boolean",
        _ => "a number",
    };

    private static string Describe(JsonNode type) => type is JsonArray several ? string.Join(" or ", several.Select(static t => t?.ToString())) : type.ToString();

    private static string? Str(JsonObject rules, string name) => rules[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static double? Number(JsonObject rules, string name) => rules[name] is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;

    private static string Escape(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static string Clip(string text) => text.Length <= 40 ? text : text[..40] + "...";

    private static IEnumerable<string> Required(JsonObject rules) =>
        rules["required"] is JsonArray names ? names.Select(static n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>() : [];
}
