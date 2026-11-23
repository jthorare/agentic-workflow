namespace Aw.Sindri.Core.Graph.Templates;

internal static partial class StyleRuleProposer
{
    /// <summary>
    /// 'vet', once per finding: keeps the model's answer only when its kind is analyzer, archunit or banned-symbol, the check id fits that kind, the slug is lowercase words joined by '-',
    /// the section is one the catalog has, the text and why are not empty, and no existing record has that slug, lists that check id or is the answer's duplicateOf. Anything else is
    /// kind 'none' with a reason: not_checkable (another kind), malformed (a field that does not fit) or duplicate. A duplicateOf that names no record is ignored.
    /// Output: kind, checkId, slug, section, text, why, reason.
    /// </summary>
    public static string VetCode { get; } = Lines(
    [
        "var doc = (input is JsonValue packed && packed.TryGetValue<string>(out var json) ? JsonNode.Parse(json) : input) as JsonObject;",
        "var answer = doc?[\"answer\"] as JsonObject;",
        "var kind = Field(answer, \"kind\");",
        "if (kind is not (\"analyzer\" or \"archunit\" or \"banned-symbol\"))",
        "{",
        "    return None(\"not_checkable\");",
        "}",
        "",
        "var (checkId, slug, section) = (Field(answer, \"checkId\"), Field(answer, \"slug\"), Field(answer, \"section\"));",
        "var (text, why, duplicate) = (Field(answer, \"text\"), Field(answer, \"why\"), Field(answer, \"duplicateOf\"));",
        "var ids = new HashSet<string>(StringComparer.Ordinal);",
        "var checkIds = new HashSet<string>(StringComparer.Ordinal);",
        "foreach (var item in doc?[\"rules\"] as JsonArray ?? new JsonArray())",
        "{",
        "    var rule = item as JsonObject;",
        "    _ = Str(rule?[\"id\"]) is { Length: > 0 } id && ids.Add(id);",
        "    foreach (var check in rule?[\"checks\"] as JsonArray ?? new JsonArray())",
        "    {",
        "        _ = Str((check as JsonObject)?[\"id\"]) is { Length: > 0 } checkName && checkIds.Add(checkName);",
        "    }",
        "}",
        "",
        "var sections = new HashSet<string>((doc?[\"sections\"] as JsonArray ?? new JsonArray()).Select(static s => Str(s) ?? string.Empty), StringComparer.Ordinal);",
        "if (!Fits(kind, checkId) || !Regex.IsMatch(slug, \"^[a-z0-9]+(-[a-z0-9]+)*$\") || !sections.Contains(section) || text.Length == 0 || why.Length == 0)",
        "{",
        "    return None(\"malformed\");",
        "}",
        "",
        "if (ids.Contains(\"engineering.csharp.\" + slug) || checkIds.Contains(checkId) || (duplicate.Length > 0 && ids.Contains(duplicate)))",
        "{",
        "    return None(\"duplicate\");",
        "}",
        "",
        "return new JsonObject { [\"kind\"] = kind, [\"checkId\"] = checkId, [\"slug\"] = slug, [\"section\"] = section, [\"text\"] = Cut(text, 600), [\"why\"] = Cut(why, 500), [\"reason\"] = string.Empty };",
        "",
        "static string? Str(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;",
        "",
        "static string Field(JsonObject? from, string name) => (Str(from?[name]) ?? string.Empty).Trim();",
        "",
        "static string Cut(string text, int chars) => text.Length > chars ? text[..chars] : text;",
        "",
        "static bool Fits(string kind, string checkId) => kind switch",
        "{",
        "    \"analyzer\" => Regex.IsMatch(checkId, \"^(CA|IDE|MA|RCS)[0-9]{4}$\"),",
        "    \"archunit\" => Regex.IsMatch(checkId, \"^ARCH-[A-Z]+-[0-9]{3}$\"),",
        "    _ => checkId.Length is > 0 and <= 300 && !checkId.Any(char.IsWhiteSpace),",
        "};",
        "",
        "static JsonObject None(string reason) => new() { [\"kind\"] = \"none\", [\"checkId\"] = string.Empty, [\"slug\"] = string.Empty, [\"section\"] = string.Empty, [\"text\"] = string.Empty, [\"why\"] = string.Empty, [\"reason\"] = reason };",
    ]);
}
