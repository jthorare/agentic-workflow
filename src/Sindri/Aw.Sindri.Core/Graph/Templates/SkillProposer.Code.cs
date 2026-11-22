namespace Aw.Sindri.Core.Graph.Templates;

internal static partial class SkillProposer
{
    /// <summary>
    /// 'check', once at the start: fails the run, naming what is missing, when the payload has no 'repo' (the toolkit repo's name in the profile, where the
    /// agent works) or no 'findings' list, before any model or agent is asked. Output: repo, findings (the count).
    /// </summary>
    public static string CheckCode { get; } = Lines(
    [
        "var payload = input as JsonObject;",
        "var repo = payload?[\"repo\"] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : string.Empty;",
        "if (repo.Length == 0)",
        "{",
        "    throw new InvalidOperationException(\"the payload needs 'repo': the toolkit repo's name in your profile, where the agent drafts the edit; put it next to 'findings'\");",
        "}",
        "",
        "if (payload?[\"findings\"] is not JsonArray findings)",
        "{",
        "    throw new InvalidOperationException(\"the payload needs 'findings': the list a steering-miner run's rank step hands on\");",
        "}",
        "",
        "return new JsonObject { [\"repo\"] = repo, [\"findings\"] = findings.Count };",
    ]);

    /// <summary>
    /// 'pick', once per finding: keeps the matched skill only when the catalog has that exact name, else 'none'; the reason is cut to 300 characters. Output: skill, why.
    /// </summary>
    public static string PickCode { get; } = Lines(
    [
        "var doc = (input is JsonValue packed && packed.TryGetValue<string>(out var json) ? JsonNode.Parse(json) : input) as JsonObject;",
        "var names = new HashSet<string>(StringComparer.Ordinal);",
        "foreach (var item in doc?[\"skills\"] as JsonArray ?? new JsonArray())",
        "{",
        "    if (Str((item as JsonObject)?[\"name\"]) is { Length: > 0 } name)",
        "    {",
        "        _ = names.Add(name);",
        "    }",
        "}",
        "",
        "var answer = doc?[\"answer\"] as JsonObject;",
        "var skill = Str(answer?[\"skill\"])?.Trim() ?? string.Empty;",
        "var why = Str(answer?[\"why\"])?.Trim() ?? string.Empty;",
        "return new JsonObject { [\"skill\"] = names.Contains(skill) ? skill : \"none\", [\"why\"] = why.Length > 300 ? why[..300] : why };",
        "",
        "static string? Str(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;",
    ]);

    /// <summary>
    /// 'entry', once per draft: the skill, the agent's report (title, files, checks; malformed files dropped, at most 20) and the branch and commit the draft is on. Output: skill, title, files, checks, branch, commit.
    /// </summary>
    public static string EntryCode { get; } = Lines(
    [
        "var doc = (input is JsonValue packed && packed.TryGetValue<string>(out var json) ? JsonNode.Parse(json) : input) as JsonObject;",
        "var report = doc?[\"report\"] as JsonObject;",
        "var files = new JsonArray();",
        "foreach (var file in report?[\"files\"] as JsonArray ?? new JsonArray())",
        "{",
        "    if (Str(file) is { Length: > 0 and <= 200 } path && files.Count < 20)",
        "    {",
        "        files.Add((JsonNode)path);",
        "    }",
        "}",
        "",
        "return new JsonObject",
        "{",
        "    [\"skill\"] = Str((doc?[\"pick\"] as JsonObject)?[\"skill\"]) ?? \"none\",",
        "    [\"title\"] = Cut(Str(report?[\"title\"]), 200),",
        "    [\"files\"] = files,",
        "    [\"checks\"] = Cut(Str(report?[\"checks\"]), 500),",
        "    [\"branch\"] = Str(doc?[\"branch\"]) ?? string.Empty,",
        "    [\"commit\"] = Str(doc?[\"commit\"]) ?? string.Empty,",
        "};",
        "",
        "static string? Str(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;",
        "",
        "static string Cut(string? text, int chars) => text is null ? string.Empty : text.Length > chars ? text[..chars] : text;",
    ]);

    /// <summary>
    /// 'summary', once at the end: one line per draft and how many findings had no skill. Output: count, text.
    /// </summary>
    public static string SummaryCode { get; } = Lines(
    [
        "var doc = (input is JsonValue packed && packed.TryGetValue<string>(out var json) ? JsonNode.Parse(json) : input) as JsonObject;",
        "var drafts = doc?[\"drafts\"] as JsonArray ?? new JsonArray();",
        "var findings = doc?[\"findings\"] is JsonValue count && count.TryGetValue<int>(out var n) ? n : 0;",
        "var lines = new List<string>();",
        "foreach (var draft in drafts)",
        "{",
        "    var row = draft as JsonObject;",
        "    lines.Add($\"- {Str(row?[\"skill\"])}: {Str(row?[\"title\"])} (branch {Str(row?[\"branch\"])})\");",
        "}",
        "",
        "var missing = Math.Max(0, findings - drafts.Count);",
        "if (missing > 0)",
        "{",
        "    lines.Add(missing == 1 ? \"1 finding had no skill\" : $\"{missing} findings had no skill\");",
        "}",
        "",
        "if (lines.Count == 0)",
        "{",
        "    lines.Add(\"No finding to draft.\");",
        "}",
        "",
        "return new JsonObject { [\"count\"] = drafts.Count, [\"text\"] = string.Join(\"\\n\", lines) };",
        "",
        "static string? Str(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;",
    ]);

    // why: the bodies are kept as lines joined with "\n", never a multi-line literal (MA0136), so the text the runner compiles is the same on every OS
    private static string Lines(string[] lines) => string.Join('\n', lines);
}
