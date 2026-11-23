namespace Aw.Sindri.Core.Graph.Templates;

internal static partial class StyleRuleProposer
{
    /// <summary>
    /// 'check', once at the start: fails the run, naming what is missing, when the payload has no 'repo' (the toolkit repo's name in the profile, where the agent
    /// works) or no 'findings' list, before any model or agent is asked. Output: repo, findings (the count of all of them), top (the first <paramref name="top"/>
    /// findings, which 'each' loops over: a for_each fails on a list longer than its maxItems, it never cuts one) and source ('steering-miner', plus ' ' and the
    /// payload's optional 'run' when that is a plain id: what the rule record cites).
    /// </summary>
    /// <param name="top">How many findings to hand on.</param>
    /// <returns>The code body.</returns>
    public static string CheckCode(int top) => Lines(
    [
        "var payload = input as JsonObject;",
        "var repo = Str(payload?[\"repo\"])?.Trim() ?? string.Empty;",
        "if (repo.Length == 0)",
        "{",
        "    throw new InvalidOperationException(\"the payload needs 'repo': the toolkit repo's name in your profile, where the agent drafts the rule; put it next to 'findings'\");",
        "}",
        "",
        "if (payload?[\"findings\"] is not JsonArray findings)",
        "{",
        "    throw new InvalidOperationException(\"the payload needs 'findings': the list a steering-miner run's rank step hands on\");",
        "}",
        "",
        "var first = new JsonArray();",
        $"for (var i = 0; i < findings.Count && i < {top.ToString(System.Globalization.CultureInfo.InvariantCulture)}; i++)",
        "{",
        "    first.Add(findings[i]?.DeepClone());",
        "}",
        "",
        "var run = Str(payload?[\"run\"])?.Trim() ?? string.Empty;",
        "var source = Regex.IsMatch(run, \"^[A-Za-z0-9_.:-]{1,80}$\") ? \"steering-miner \" + run : \"steering-miner\";",
        "return new JsonObject { [\"repo\"] = repo, [\"findings\"] = findings.Count, [\"top\"] = first, [\"source\"] = source };",
        "",
        "static string? Str(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;",
    ]);

    /// <summary>
    /// 'entry', once per draft: the rule and check the draft is for, the agent's report (title, rationale, files, checks, violations on main; malformed parts dropped, at most 20 files)
    /// and the branch and commit the draft is on. Output: ruleId, checkId, kind, title, rationale, files, checks, violationsOnMain (a number or null), branch, commit.
    /// </summary>
    public static string EntryCode { get; } = Lines(
    [
        "var doc = (input is JsonValue packed && packed.TryGetValue<string>(out var json) ? JsonNode.Parse(json) : input) as JsonObject;",
        "var vet = doc?[\"vet\"] as JsonObject;",
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
        "var rawChecks = report?[\"checks\"];",
        "var checks = rawChecks is JsonValue ? Str(rawChecks) : rawChecks?.ToJsonString();",
        "JsonNode? violations = report?[\"violationsOnMain\"] is JsonValue count && count.TryGetValue<int>(out var n) && n >= 0 ? JsonValue.Create(n) : null;",
        "return new JsonObject",
        "{",
        "    [\"ruleId\"] = \"engineering.csharp.\" + (Str(vet?[\"slug\"]) ?? string.Empty),",
        "    [\"checkId\"] = Str(vet?[\"checkId\"]) ?? string.Empty,",
        "    [\"kind\"] = Str(vet?[\"kind\"]) ?? string.Empty,",
        "    [\"title\"] = Cut(Str(report?[\"title\"]), 200),",
        "    [\"rationale\"] = Cut(Str(report?[\"rationale\"]), 500),",
        "    [\"files\"] = files,",
        "    [\"checks\"] = Cut(checks, 500),",
        "    [\"violationsOnMain\"] = violations,",
        "    [\"branch\"] = Str(doc?[\"branch\"]) ?? string.Empty,",
        "    [\"commit\"] = Str(doc?[\"commit\"]) ?? string.Empty,",
        "};",
        "",
        "static string? Str(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;",
        "",
        "static string Cut(string? text, int chars) => text is null ? string.Empty : text.Length > chars ? text[..chars] : text;",
    ]);

    /// <summary>
    /// 'summary', once at the end: one line per draft, how many of the looped findings got no draft, and how many findings were past the top ones. Output: count, text.
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
        "    lines.Add($\"- {Str(row?[\"ruleId\"])}: {Str(row?[\"title\"])} (branch {Str(row?[\"branch\"])})\");",
        "}",
        "",
        "var given = doc?[\"given\"] is JsonValue all && all.TryGetValue<int>(out var g) ? g : findings;",
        "var missing = Math.Max(0, findings - drafts.Count);",
        "if (missing > 0)",
        "{",
        "    lines.Add(missing == 1 ? \"1 finding got no draft: no check can enforce it, an existing rule covers it, or the answer was malformed\" : $\"{missing} findings got no draft: no check can enforce them, an existing rule covers them, or the answer was malformed\");",
        "}",
        "",
        "if (given > findings)",
        "{",
        "    lines.Add(given - findings == 1 ? $\"1 more finding was past the top {findings} and got no draft\" : $\"{given - findings} more findings were past the top {findings} and got no draft\");",
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
