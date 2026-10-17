using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// Reads launcher.json (launcher spec §4, schemas/launcher.schema.json) over the built-in entries. This build reads 'agents' and 'sessions';
/// 'viewer', 'host' and 'meta' are checked (unknown keys and kinds are errors) and not read, until SL-2 reads them.
/// </summary>
internal static class LauncherConfigFile
{
    internal static readonly System.Text.Json.JsonSerializerOptions Json = AwJson.CreateOptions(LauncherJsonContext.Default);

    private static readonly string[] _viewerKinds = ["warp", "exec-template", "print"];
    private static readonly string[] _hostKinds = ["tmux", "none"];

    /// <summary>The catalog and the sessions section from the built-ins and an optional user file.</summary>
    /// <param name="builtins">The built-in entries (config/launcher/builtins.json).</param>
    /// <param name="user">The text of launcher.json, or null when the box has none.</param>
    /// <returns>The config, or SND-LAUNCH-011 saying what is wrong.</returns>
    internal static Result<LauncherConfig> Read(byte[] builtins, byte[]? user)
    {
        var baseline = Parse(builtins);
        if (baseline.IsError)
        {
            return baseline.Error;
        }

        var file = user is null ? Results.Success<LauncherDocument?>(value: null) : Parse(user).Map(static d => (LauncherDocument?)d);
        if (file.IsError)
        {
            return file.Error;
        }

        var agents = Agents(baseline.Value.Agents ?? [], file.Value?.Agents);
        return agents.IsError ? agents.Error : new LauncherConfig(agents.Value, file.Value?.Sessions) { Viewer = file.Value?.Viewer, Host = file.Value?.Host };
    }

    private static Result<LauncherDocument> Parse(byte[] bytes)
    {
        var read = AwJson.Deserialize<LauncherDocument>(bytes, Json);
        if (read.IsError)
        {
            return SindriErrors.LauncherInvalid.With(read.Error.Message);
        }

        var problem = Problem(read.Value);
        return problem is null ? read.Value : SindriErrors.LauncherInvalid.With(problem);
    }

    private static string? Problem(LauncherDocument d) =>
        d.Schema != 1 ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"schema {d.Schema} isn't supported; this build reads 1")
        : Viewer(d.Viewer) ?? Host(d.Host) ?? Meta(d.Meta) ?? Sessions(d.Sessions);

    internal static string? Viewer(ViewerSection? v)
    {
        if (v is null)
        {
            return null;
        }

        if (!_viewerKinds.Contains(v.Kind, StringComparer.Ordinal))
        {
            return $"viewer.kind '{v.Kind}' isn't one of {string.Join(", ", _viewerKinds)}";
        }

        var template = v.Kind == "exec-template";
        return template != (v.Name is { Length: > 0 } && v.Argv is { Count: > 0 }) || (!template && (v.Name is not null || v.Argv is not null))
            ? "viewer: exec-template needs name and argv; warp and print take neither"
            : AgentSections.Argv(v.Argv, "viewer.argv");
    }

    internal static string? Host(HostSection? h) =>
        h is null || _hostKinds.Contains(h.Kind, StringComparer.Ordinal) ? null : $"host.kind '{h.Kind}' isn't one of {string.Join(", ", _hostKinds)}";

    private static string? Meta(MetaSection? m)
    {
        var ids = m?.Lanes is { } lanes ? new List<string>(lanes.Values) : [];
        if (m?.LanesAgent is { } lead)
        {
            ids.Add(lead);
        }

        return ids.Find(static id => !AgentId.TryFrom(id, out _)) is { } bad ? $"meta: '{bad}' isn't an agent id" : null;
    }

    internal static string? Sessions(SessionsSection? s)
    {
        if (s is null)
        {
            return null;
        }

        return s.PollSeconds is < 1 or > 30
            ? "sessions.pollSeconds is 1 to 30"
            : (s.Providers ?? []).FirstOrDefault(static id => !AgentId.TryFrom(id, out _)) is { } bad ? $"sessions.providers: '{bad}' isn't an agent id" : null;
    }

    private static Result<IReadOnlyList<AgentEntry>> Agents(Dictionary<string, AgentSection> builtins, Dictionary<string, AgentSection>? added)
    {
        var merged = new Dictionary<string, AgentSection>(builtins, StringComparer.Ordinal);
        foreach (var (id, section) in added ?? [])
        {
            var builtIn = builtins.TryGetValue(id, out var baseline);
            if (section.Override == true != builtIn)
            {
                return SindriErrors.LauncherInvalid.With($"agents.{id}: " + (builtIn ? "a built-in id needs \"override\": true" : "\"override\": true is for a built-in id"));
            }

            merged[id] = builtIn ? AgentSections.Merge(baseline!, section) : section; // why: TryGetValue returned true
        }

        var entries = new List<AgentEntry>();
        foreach (var (id, section) in merged)
        {
            var entry = AgentSections.Entry(id, section, AgentSections.SourceOf(id, builtins, added));
            if (entry.IsError)
            {
                return entry.Error;
            }

            entries.Add(entry.Value);
        }

        return entries;
    }

    internal static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
}
