using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// Reads launcher.json one section at a time (launcher spec §4): a mistake in one section costs that section only. 'meta' is ignored whatever it holds;
/// 'viewer' and 'host' problems are recorded for attended opens and never reach the collector; a bad 'agents.&lt;id&gt;' entry drops itself; a bad
/// 'sessions' section falls back to the defaults, except that an explicit "enabled": false still holds, so a typo never turns the collector on. A file
/// that isn't a JSON object, or whose schema isn't 1, gives the built-ins and the defaults.
/// </summary>
internal static class IsolatedLauncherRead
{
    private static readonly string[] _topLevel = ["schema", "viewer", "host", "agents", "meta", "sessions"];

    /// <summary>The catalog and the sessions section from the built-ins and an optional user file.</summary>
    /// <param name="builtins">The built-in entries.</param>
    /// <param name="user">The text of launcher.json, or null when the box has none.</param>
    /// <returns>The config; its problems say what was dropped.</returns>
    internal static LauncherConfig Read(byte[] builtins, byte[]? user)
    {
        var baseline = LauncherConfigFile.Read(builtins, user: null).Value;
        if (user is null)
        {
            return baseline;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(user);
        }
        catch (JsonException e)
        {
            return baseline with { Problem = "launcher.json isn't valid JSON", Unusable = true, ProblemLine = (int)(e.LineNumber ?? -1) + 1 };
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1)
            {
                return baseline with { Problem = "launcher.json needs \"schema\": 1", Unusable = true };
            }

            return Sections(builtins, user, root);
        }
    }

    private static LauncherConfig Sections(byte[] builtins, byte[] user, JsonElement root)
    {
        var problems = new List<string>();
        var attended = new List<string>();
        foreach (var name in root.EnumerateObject().Select(static p => p.Name).Where(static n => !_topLevel.Contains(n, StringComparer.Ordinal)))
        {
            problems.Add($"unknown key '{name}'");
        }

        var viewer = Attended<ViewerSection>(root, "viewer", LauncherConfigFile.Viewer, attended);
        var host = Attended<HostSection>(root, "host", LauncherConfigFile.Host, attended);
        var sessions = Sessions(root, problems);
        var dropped = new List<DroppedEntry>();
        var agents = Agents(builtins, user, root, problems, dropped);
        return new LauncherConfig(agents, sessions) { Problem = problems.Count == 0 ? null : string.Join("; ", problems), AttendedProblems = attended, Dropped = dropped, Viewer = viewer, Host = host };
    }

    private static T? Attended<T>(JsonElement root, string name, Func<T?, string?> check, List<string> attended)
        where T : class
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return null;
        }

        var section = Section<T>(element);
        var problem = section is null ? $"{name} isn't valid" : check(section);
        if (problem is null)
        {
            return section;
        }

        attended.Add(problem);
        return null;
    }

    private static SessionsSection? Sessions(JsonElement root, List<string> problems)
    {
        if (!root.TryGetProperty("sessions", out var element))
        {
            return null;
        }

        var section = Section<SessionsSection>(element);
        if (section is not null && LauncherConfigFile.Sessions(section) is null)
        {
            return section;
        }

        problems.Add("sessions isn't valid; the defaults are used");
        var stayOff = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.False;
        return stayOff ? new SessionsSection { Enabled = false } : null;
    }

    private static List<AgentEntry> Agents(byte[] builtins, byte[] user, JsonElement root, List<string> problems, List<DroppedEntry> dropped)
    {
        var lines = AgentLines.Of(user);
        var named = new Dictionary<string, AgentSection>(StringComparer.Ordinal);
        var sink = new Problems(problems, dropped, lines);
        var baselineAgents = AwJson.Deserialize<LauncherDocument>(builtins, LauncherConfigFile.Json).Value.Agents ?? [];
        var merged = new Dictionary<string, AgentSection>(baselineAgents, StringComparer.Ordinal);
        if (root.TryGetProperty("agents", out var added) && added.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in added.EnumerateObject())
            {
                Add(property, baselineAgents, merged, sink, named);
            }
        }
        else if (root.TryGetProperty("agents", out _))
        {
            problems.Add("agents isn't an object");
        }

        var entries = new List<AgentEntry>();
        foreach (var (id, section) in merged)
        {
            var entry = AgentSections.Entry(id, section, AgentSections.SourceOf(id, baselineAgents, named));
            if (entry.IsSuccess)
            {
                entries.Add(entry.Value);
            }
            else
            {
                problems.Add(entry.Error.Message);
            }
        }

        return entries;
    }

    private sealed record Problems(List<string> Messages, List<DroppedEntry> Dropped, Dictionary<string, int> Lines)
    {
        internal void Drop(string id, string reason)
        {
            Messages.Add(reason);
            Dropped.Add(new DroppedEntry(id, Lines.GetValueOrDefault(id), reason));
        }
    }

    private static void Add(JsonProperty property, Dictionary<string, AgentSection> baselineAgents, Dictionary<string, AgentSection> merged, Problems problems, Dictionary<string, AgentSection> named)
    {
        var id = property.Name;
        var section = Section<AgentSection>(property.Value);
        var builtIn = baselineAgents.TryGetValue(id, out var baseline);
        if (section is null)
        {
            problems.Drop(id, $"agents.{id} isn't valid");
        }
        else if (section.Override == true != builtIn)
        {
            problems.Drop(id, $"agents.{id}: " + (builtIn ? "a built-in id needs \"override\": true" : "\"override\": true is for a built-in id"));
        }
        else
        {
            var candidate = builtIn ? AgentSections.Merge(baseline!, section) : section; // why: TryGetValue returned true
            var checkedEntry = AgentSections.Entry(id, candidate);
            if (checkedEntry.IsError)
            {
                problems.Drop(id, checkedEntry.Error.Message);
            }
            else
            {
                merged[id] = candidate;
                named[id] = section;
            }
        }
    }

    private static T? Section<T>(JsonElement element)
        where T : class
    {
        var read = AwJson.Deserialize<T>(System.Text.Encoding.UTF8.GetBytes(element.GetRawText()), LauncherConfigFile.Json);
        return read.IsError ? null : read.Value;
    }
}
