using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// A 'run_agent' node's config, read once: the repo the session works in, the repos it may read, the agent, the prompt, the output fields it must report, how long the node may take,
/// the connections it names and the sandbox size.
/// </summary>
/// <param name="Repo">The profile repo the session works in (a fresh worktree on its own branch).</param>
/// <param name="ReadOnlyRepos">Repos the session may read and never write.</param>
/// <param name="Agent">'claude-code', 'codex' or 'cursor', or null for the profile's agent.</param>
/// <param name="Prompt">The interpolated prompt.</param>
/// <param name="Outputs">The declared output fields by name, each with its type ('string', 'number', 'boolean', 'object' or 'array'; a trailing '?' marks an optional one).</param>
/// <param name="Timeout">How long the node may run (taken-over time excluded).</param>
/// <param name="Connections">The connections the node names; each one adds its sandbox host group (PR 10).</param>
/// <param name="Wall">The sandbox's wall-clock limit from 'resources.wall', or null for the default.</param>
public sealed partial record RunAgentConfig(string Repo, IReadOnlyList<string> ReadOnlyRepos, string? Agent, string Prompt, IReadOnlyDictionary<string, string> Outputs, TimeSpan Timeout, IReadOnlyList<string> Connections, TimeSpan? Wall)
{
    private static readonly string[] _types = ["string", "number", "boolean", "object", "array"];

    /// <summary>The agents a node may name.</summary>
    public static IReadOnlyList<string> Agents { get; } = ["claude-code", "codex", "cursor"];

    /// <summary>How long a node runs when its config gives no 'timeout'.</summary>
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromHours(2);

    /// <summary>Reads a node's interpolated config.</summary>
    /// <param name="config">The config, with every reference replaced.</param>
    /// <returns>The config, or why it can't be used.</returns>
    public static Result<RunAgentConfig> Parse(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (Text(config["repo"]) is not { Length: > 0 } repo)
        {
            return SindriErrors.GraphBlockFailed.With("run_agent needs a 'repo'");
        }

        if (Text(config["prompt"]) is not { } prompt)
        {
            return SindriErrors.GraphBlockFailed.With("run_agent needs a 'prompt'");
        }

        var agent = Text(config["agent"]);
        if (agent is not null && !Agents.Contains(agent, StringComparer.Ordinal))
        {
            return SindriErrors.GraphBlockFailed.With($"run_agent's 'agent' is '{agent}'; use one of {string.Join(", ", Agents)}");
        }

        var (timeout, timeoutBad) = Duration(config["timeout"]);
        var (wall, wallBad) = Duration((config["resources"] as JsonObject)?["wall"]);
        return timeoutBad || wallBad
            ? SindriErrors.GraphBlockFailed.With("run_agent's 'timeout' and 'resources.wall' are a whole number and m, h or d, such as '45m'")
            : new RunAgentConfig(repo, Strings(config["readOnlyRepos"]), agent, prompt, DeclaredOutputs(config), timeout ?? DefaultTimeout, Strings(config["connections"]), wall);
    }

    /// <summary>Problems in a node's config that the field types can't express: a bad agent, a bad output name or type, a reserved name, a bad duration.</summary>
    /// <param name="config">The node's config, as written.</param>
    /// <returns>One problem per field: the field's name and what to change.</returns>
    public static IReadOnlyList<(string Field, string Problem)> Problems(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var problems = new List<(string Field, string Problem)>();
        if (Text(config["agent"]) is { } agent && !Reference(agent) && !Agents.Contains(agent, StringComparer.Ordinal))
        {
            problems.Add(("agent", $"is '{agent}'; use one of {string.Join(", ", Agents)}"));
        }

        if (string.Equals(Text(config["agent"]), "codex", StringComparison.Ordinal) && Strings(config["readOnlyRepos"]).Count > 0)
        {
            problems.Add(("readOnlyRepos", "can't be used with agent 'codex': its policy form has no read-only paths (SND-LAUNCH-013 at launch); use claude-code or cursor, or drop readOnlyRepos"));
        }

        problems.AddRange((config["outputs"] as JsonObject ?? []).Select(o => OutputProblem(o.Key, Text(o.Value))).OfType<string>().Select(static p => ("outputs", p)));
        if (DurationProblem(config["timeout"]) is { } timeout)
        {
            problems.Add(("timeout", timeout));
        }

        if (DurationProblem((config["resources"] as JsonObject)?["wall"]) is { } wall)
        {
            problems.Add(("resources", "'wall' " + wall));
        }

        return problems;
    }

    private static string? DurationProblem(JsonNode? node) =>
        Text(node) is { } text && !Reference(text) && !RecipeDuration.TryFrom(text, out _) ? $"is '{text}'; write a whole number and m, h or d, such as '45m'" : null;

    /// <summary>The output fields a node declares, by name with their types.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>The fields; none when the config declares none.</returns>
    public static IReadOnlyDictionary<string, string> DeclaredOutputs(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, type) in config["outputs"] as JsonObject ?? [])
        {
            outputs[name] = Text(type) ?? string.Empty;
        }

        return outputs;
    }

    private static string? OutputProblem(string name, string? type)
    {
        if (!FieldName.IsMatch(name))
        {
            return $"'{name}' is not a field name; start with a lowercase letter, then letters and digits only (the agent reports it as fact:{name})";
        }

        if (RunAgentBlock.Standard.ContainsKey(name))
        {
            return $"'{name}' is a field every run_agent node hands on already; pick another name";
        }

        return type is not null && _types.Contains(type.TrimEnd('?'), StringComparer.Ordinal)
            ? null
            : $"'{name}' has the type {(type is null ? "(not text)" : $"'{type}'")}; use string, number, boolean, object or array, and a trailing '?' for an optional field";
    }

    private static bool Reference(string text) => text.Contains("{{", StringComparison.Ordinal);

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static IReadOnlyList<string> Strings(JsonNode? node) => node is JsonArray list ? [.. list.Select(Text).OfType<string>()] : [];

    private static (TimeSpan? Value, bool Bad) Duration(JsonNode? node) =>
        Text(node) is not { } text ? (null, false) : RecipeDuration.TryFrom(text, out var duration) ? (duration.Span, false) : (null, true);

    [GeneratedRegex("^[a-z][A-Za-z0-9]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FieldName { get; }
}
