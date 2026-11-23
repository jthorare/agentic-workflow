using System.Globalization;
using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The words on a workflow card (#3684): line 1 is the step's title or, with no title, the block's name (never the id); line 2 is one settings line per block kind,
/// made from the node's config as the workflow read serves it. A pure function: no Avalonia, no API calls.
/// </summary>
internal static class GraphCardLine
{
    // The catalog's block titles (Aw.Sindri.Core/Graph/Blocks/*Block.cs Title): the name when the server sent no kindTitle for a node.
    private static readonly Dictionary<string, string> _names = new(StringComparer.Ordinal)
    {
        ["manual"] = "When called",
        ["schedule"] = "Schedule",
        ["connection_event"] = "Connection event",
        ["code"] = "Code",
        ["condition"] = "If",
        ["http"] = "HTTP request",
        ["notify"] = "Send notification",
        ["wait"] = "Wait",
        ["collect"] = "Collect",
        ["prompt"] = "Prompt",
        ["create_item"] = "Create item",
        ["run_agent"] = "Run agent",
        ["run_workflow"] = "Run workflow",
        ["for_each"] = "For each",
        ["call_tool"] = "Toolkit tool",
        ["ask_owner"] = "Ask owner",
        ["only_new"] = "Only new",
        ["memory_search"] = "Recall",
        ["memory_write"] = "Remember",
        ["memory_forget"] = "Forget",
    };

    /// <summary>The block's name: the served kind title, else the catalog's title for the type, else the type itself with its underscores as spaces.</summary>
    /// <param name="type">The block type, such as "code".</param>
    /// <param name="kindTitle">The kind title the workflow read served for the node, or null.</param>
    /// <returns>The name, never empty for a non-empty type.</returns>
    internal static string KindName(string type, string? kindTitle) =>
        kindTitle is { Length: > 0 } served ? served
        : _names.TryGetValue(type, out var name) ? name
        : type.Replace('_', ' ');

    /// <summary>Card line 1: the title in the person's words, or the block's name when the step has none.</summary>
    /// <param name="title">The node's title as served (may be empty).</param>
    /// <param name="type">The block type.</param>
    /// <param name="kindTitle">The served kind title, or null.</param>
    /// <returns>The line.</returns>
    internal static string Title(string title, string type, string? kindTitle) => title.Trim().Length > 0 ? title.Trim() : KindName(type, kindTitle);

    /// <summary>Card line 2: one settings line for the block kind. The schedule's line is the server's (it knows the time zone rules); the other kinds read their config.
    /// When a kind's settings are missing the line is the block's name.</summary>
    /// <param name="type">The block type.</param>
    /// <param name="config">The node's "config" object as served, or default when there is none.</param>
    /// <param name="summary">The summary line the workflow read served for the node, or null.</param>
    /// <param name="kindTitle">The served kind title, or null.</param>
    /// <returns>The line, never empty.</returns>
    internal static string Settings(string type, JsonElement config, string? summary, string? kindTitle)
    {
        var name = KindName(type, kindTitle);
        var line = string.Equals(type, "schedule", StringComparison.Ordinal)
            ? (summary is { Length: > 0 } && !string.Equals(summary, name, StringComparison.Ordinal) ? summary : null)
            : _lines.TryGetValue(type, out var read) ? read(config) : null;
        return line ?? (summary is { Length: > 0 } ? summary : name);
    }

    // One reader per block kind: the settings line from the config, or null when the setting it needs is missing.
    private static readonly Dictionary<string, Func<JsonElement, string?>> _lines = new(StringComparer.Ordinal)
    {
        ["manual"] = static _ => "From Run now or the API",
        ["code"] = Code,
        ["condition"] = static c => Prefixed("If ", Str(c, "when")),
        ["http"] = Http,
        ["notify"] = static c => Prefixed("Notify: ", Str(c, "title")),
        ["wait"] = Wait,
        ["collect"] = static c => Prefixed("Collect ", Str(c, "items")),
        ["prompt"] = Prompt,
        ["create_item"] = static c => "Create item in " + (Str(c, "repo") ?? "the queue"),
        ["run_agent"] = RunAgent,
        ["run_workflow"] = static c => Prefixed("Run workflow ", Str(c, "workflow")),
        ["for_each"] = static c => Prefixed("For each of ", Str(c, "items")),
        ["call_tool"] = CallTool,
        ["ask_owner"] = static c => Prefixed("Ask: ", Str(c, "question")),
    };

    private static string? Prefixed(string prefix, string? value) => value is null ? null : prefix + value;

    private static string? Http(JsonElement config) =>
        Str(config, "url") is { } url ? (Str(config, "method") ?? "GET").ToUpperInvariant() + " " + Host(url) : null;

    private static string? Prompt(JsonElement config)
    {
        if (config.ValueKind == JsonValueKind.Object && config.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Object)
        {
            return "Prompt, answer as fields";
        }

        return Str(config, "prompt") is null ? null : "Prompt, answer in text";
    }

    private static string? RunAgent(JsonElement config) =>
        Str(config, "agent") is { } agent ? "Run " + agent + (Str(config, "repo") is { } repo ? " in " + repo : string.Empty) : null;

    private static string? CallTool(JsonElement config) =>
        Str(config, "tool") is { } tool ? (Str(config, "toolkit") is { } kit ? kit + " · " + tool : tool) : null;

    private static string? Code(JsonElement config)
    {
        if (Str(config, "code") is not { } code)
        {
            return null;
        }

        var lines = code.Split('\n').Count(l => l.Trim().Length > 0);
        return string.Create(CultureInfo.InvariantCulture, $"JavaScript · {lines} {(lines == 1 ? "line" : "lines")}");
    }

    private static string? Wait(JsonElement config) =>
        Str(config, "duration") is { } duration ? "Wait " + Duration(duration)
        : Str(config, "until") is { } until ? "Wait until " + until + (Str(config, "timezone") is { } zone ? " " + zone : string.Empty)
        : null;

    // "15m" reads "15 minutes"; a value it can't read is shown as written.
    private static string Duration(string text)
    {
        var t = text.Trim();
        if (t.Length < 2 || !int.TryParse(t[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            return t;
        }

        var unit = t[^1] switch { 's' => "second", 'm' => "minute", 'h' => "hour", 'd' => "day", _ => null };
        return unit is null ? t : string.Create(CultureInfo.InvariantCulture, $"{n} {unit}{(n == 1 ? string.Empty : "s")}");
    }

    // The URL without its scheme: "https://status.example.com/api" reads "status.example.com/api".
    private static string Host(string url)
    {
        var at = url.IndexOf("://", StringComparison.Ordinal);
        return at < 0 ? url : url[(at + 3)..];
    }

    private static string? Str(JsonElement config, string name) =>
        config.ValueKind == JsonValueKind.Object && config.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } s && s.Trim().Length > 0 ? s.Trim() : null;
}
