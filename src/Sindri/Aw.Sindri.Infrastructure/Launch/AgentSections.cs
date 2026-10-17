using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Merges an override over a built-in agent entry and checks the result as an entry (launcher spec §4, schemas/launcher.schema.json).</summary>
internal static partial class AgentSections
{
    private static readonly string[] _sessionIds = ["pre-chosen-uuid", "reported", "none"];
    private static readonly string[] _usages = ["claude-code", "codex", "cursor", "none"];
    private static readonly string[] _mcps = ["claude-config-file", "codex-overrides", "none"];
    private static readonly string[] _models = ["claude-flag", "codex-flag", "cursor-flag", "none"];
    private static readonly string[] _policies = ["claude-settings-file", "codex-overrides", "cursor-config", "none"];

    /// <summary>The built-in entry with <paramref name="over"/>'s named fields laid over it.</summary>
    internal static AgentSection Merge(AgentSection baseline, AgentSection over) => Elements(baseline, over) with
    {
        Label = over.Label ?? baseline.Label,
        Bin = over.Bin ?? baseline.Bin,
        Open = over.Open ?? baseline.Open,
        SessionId = over.SessionId ?? baseline.SessionId,
        Usage = over.Usage ?? baseline.Usage,
        Mcp = over.Mcp ?? baseline.Mcp,
        Override = null,
    };

    private static AgentSection Elements(AgentSection baseline, AgentSection over) => baseline with
    {
        Resume = Present(over.Resume) ? over.Resume : baseline.Resume,
        SessionIdPattern = Present(over.SessionIdPattern) ? over.SessionIdPattern : baseline.SessionIdPattern,
        Model = Present(over.Model) ? over.Model : baseline.Model,
        Policy = Present(over.Policy) ? over.Policy : baseline.Policy,
        ReportedId = Present(over.ReportedId) ? over.ReportedId : baseline.ReportedId,
        ProcessName = Present(over.ProcessName) ? over.ProcessName : baseline.ProcessName,
        Transcripts = Present(over.Transcripts) ? over.Transcripts : baseline.Transcripts,
    };

    /// <summary>Checks <paramref name="section"/> as a full entry named <paramref name="id"/> and reads the fields the collector uses.</summary>
    /// <param name="id">The entry's id.</param>
    /// <param name="section">The merged section.</param>
    /// <param name="source">Where the entry came from (launcher L-10).</param>
    /// <returns>The entry, with its forms and argv hash, or SND-LAUNCH-011.</returns>
    internal static Result<AgentEntry> Entry(string id, AgentSection section, CatalogSource source = CatalogSource.Builtin)
    {
        if (!AgentId.TryFrom(id, out var agentId))
        {
            return Bad(id, "the id isn't an agent id");
        }

        var problem = Problem(section);
        return problem is not null
            ? Bad(id, problem)
            : new AgentEntry(agentId, section.Label!, section.Bin!, Text(section.ProcessName), Text(section.Transcripts), Strings(section.Resume)) // why: Problem() proved label and bin are set
            {
                IdCapture = Capture(section.ReportedId),
                ReportedNewestFile = ReportedText(section.ReportedId, "newestFile"),
                Open = section.Open!, // why: Problem() proved open is set
                SessionIdMode = Mode(section.SessionId),
                SessionIdPattern = Text(section.SessionIdPattern),
                Usage = section.Usage,
                ModelForm = ModelOf(Text(section.Model)),
                PolicyForm = PolicyOf(Text(section.Policy)),
                Source = source,
                ArgvHash = Hash(section),
            };
    }

    /// <summary>Where entry <paramref name="id"/> came from: a built-in id the file also names is an override, an id only the file names is added.</summary>
    internal static CatalogSource SourceOf(string id, IReadOnlyDictionary<string, AgentSection> builtins, IReadOnlyDictionary<string, AgentSection>? named) =>
        !builtins.ContainsKey(id) ? CatalogSource.Added : named?.ContainsKey(id) == true ? CatalogSource.Override : CatalogSource.Builtin;

    private static string Hash(AgentSection s) => ArgvHash.Of(ArgvHash.Canonical(new HashInput(
        s.Bin!, s.Open!, Strings(s.Resume), s.SessionId!, s.Mcp ?? "none", Text(s.Model) ?? "none", Text(s.Policy) ?? "none"))); // why: Problem() proved bin, open and sessionId are set

    private static ModelForm ModelOf(string? name) => name switch
    {
        "claude-flag" => ModelForm.ClaudeFlag,
        "codex-flag" => ModelForm.CodexFlag,
        "cursor-flag" => ModelForm.CursorFlag,
        _ => ModelForm.None,
    };

    private static PolicyForm PolicyOf(string? name) => name switch
    {
        "claude-settings-file" => PolicyForm.ClaudeSettingsFile,
        "codex-overrides" => PolicyForm.CodexOverrides,
        "cursor-config" => PolicyForm.CursorConfig,
        _ => PolicyForm.None,
    };

    private static SessionIdMode Mode(string? value) => value switch
    {
        "pre-chosen-uuid" => SessionIdMode.PreChosenUuid,
        "reported" => SessionIdMode.Reported,
        _ => SessionIdMode.None,
    };

    private static string? Problem(AgentSection s)
    {
        return Required(s) ?? OneOf(s.SessionId, _sessionIds, "sessionId") ?? OneOf(s.Usage, _usages, "usage") ?? OneOf(s.Mcp, _mcps, "mcp") ?? Forms(s)
            ?? Argv(s.Open, "open") ?? Argv(Strings(s.Resume), "resume") ?? Reported(s);
    }

    private static string? Required(AgentSection s) =>
        s.Label is not { Length: > 0 } || s.Bin is not { Length: > 0 } || s.Open is not { Count: > 0 } || s.SessionId is null ? "an entry needs label, bin, open and sessionId" : null;

    private static string? Forms(AgentSection s) => Form(s.Model, _models, "model") ?? Form(s.Policy, _policies, "policy");

    private static string? Reported(AgentSection s)
    {
        if ((string.Equals(s.SessionId, "pre-chosen-uuid", StringComparison.Ordinal) || string.Equals(s.SessionId, "reported", StringComparison.Ordinal))
            && s.SessionIdPattern.ValueKind != JsonValueKind.String)
        {
            return "sessionIdPattern is required unless sessionId is none";
        }

        if (string.Equals(s.SessionId, "reported", StringComparison.Ordinal) && s.ReportedId.ValueKind != JsonValueKind.Object)
        {
            return "sessionId reported needs reportedId";
        }

        return s.ReportedId.ValueKind == JsonValueKind.Object && AwJson.Deserialize<ReportedIdSection>(
            System.Text.Encoding.UTF8.GetBytes(s.ReportedId.GetRawText()), LauncherConfigFile.Json).IsError ? "reportedId has an unknown key" : null;
    }

    private static string? Form(JsonElement value, string[] allowed, string field) =>
        !Present(value) ? null : OneOf(value.ValueKind == JsonValueKind.String ? value.GetString() : "(not a string)", allowed, field);

    private static string? OneOf(string? value, string[] allowed, string field) =>
        value is null || allowed.Contains(value, StringComparer.Ordinal) ? null : $"{field} '{value}' isn't one of {string.Join(", ", allowed)}";

    internal static string? Argv(IReadOnlyList<string>? argv, string field) =>
        argv?.FirstOrDefault(static e => !Element.IsMatch(e)) is { } bad ? $"{field}: '{bad}' isn't an argument template (closed placeholder set; {{cmd...}} is a whole element)" : null;

    private static bool Present(JsonElement element) => element.ValueKind != JsonValueKind.Undefined;

    private static string? Capture(JsonElement reported) => ReportedText(reported, "capture");

    private static string? ReportedText(JsonElement reported, string name) =>
        reported.ValueKind == JsonValueKind.Object && reported.TryGetProperty(name, out var value) ? Text(value) : null;

    private static string? Text(JsonElement element) => element.ValueKind == JsonValueKind.String ? element.GetString() : null;

    private static IReadOnlyList<string>? Strings(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array ? [.. element.EnumerateArray().Select(static e => e.GetString() ?? string.Empty)] : null;

    private static Error Bad(string id, string reason) => SindriErrors.LauncherInvalid.With($"agents.{id}: {reason}");

    [GeneratedRegex("^(?:\\{cmd\\.\\.\\.\\}|(?:[^{}]|\\{(?:bin|cwd|title|name|seed|sessionId|mcpConfig)\\})*)\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Element { get; }
}
