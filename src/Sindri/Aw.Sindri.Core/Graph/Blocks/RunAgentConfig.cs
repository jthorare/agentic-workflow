using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>A 'run_agent' node's config.</summary>
/// <param name="Repo">The repo the session works in (a profile repo name).</param>
/// <param name="ReadOnlyRepos">Repos mounted read only.</param>
/// <param name="Agent">'claude-code', 'codex' or 'cursor'.</param>
/// <param name="Prompt">The task, with references already replaced.</param>
/// <param name="Outputs">The fields the agent must report: name to type ('string', 'number', 'boolean', 'object', 'array'; a trailing '?' marks an optional field).</param>
/// <param name="Timeout">How long the session may take ('45m'), or null for the workflow's default.</param>
/// <param name="Connections">The connections the session may use; their hosts open in the sandbox.</param>
public sealed record RunAgentConfig(
    string Repo,
    IReadOnlyList<string> ReadOnlyRepos,
    string Agent,
    string Prompt,
    IReadOnlyDictionary<string, string> Outputs,
    string? Timeout,
    IReadOnlyList<string> Connections)
{
    /// <summary>Reads a node's config.</summary>
    /// <param name="config">The config.</param>
    /// <returns>The typed config, or the field that does not fit.</returns>
    public static Result<RunAgentConfig> Parse(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        foreach (var name in new[] { "repo", "agent", "prompt" })
        {
            if (Text(config[name]) is not { Length: > 0 })
            {
                return Bad($"'{name}' must be text");
            }
        }

        if (config["outputs"] is not JsonObject declared)
        {
            return Bad("'outputs' must be an object of field name to type, such as {\"drifted\":\"boolean\"}");
        }

        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, type) in declared)
        {
            if (Text(type) is not { Length: > 0 } text)
            {
                return Bad($"output '{name}' must name a type such as \"string\" or \"string?\"");
            }

            outputs[name] = text;
        }

        var readOnly = TextList(config["readOnlyRepos"], "readOnlyRepos");
        var connections = TextList(config["connections"], "connections");
        if (readOnly.IsError || connections.IsError)
        {
            return readOnly.IsError ? readOnly.Error : connections.Error;
        }

        return new RunAgentConfig(Text(config["repo"])!, readOnly.Value, Text(config["agent"])!, Text(config["prompt"])!, outputs, Text(config["timeout"]), connections.Value);   // why: the loop above proved repo, agent and prompt are non-empty text
    }

    private static Result<IReadOnlyList<string>> TextList(JsonNode? node, string name)
    {
        if (node is null)
        {
            return Results.Success<IReadOnlyList<string>>([]);
        }

        if (node is not JsonArray list || list.Any(static n => Text(n) is null))
        {
            return Bad($"'{name}' must be a list of text");
        }

        return Results.Success<IReadOnlyList<string>>([.. list.Select(static n => Text(n)!)]);   // why: the check above proved every entry is text
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static Error Bad(string message) => SindriErrors.GraphBlockFailed.With($"run_agent: {message}");
}
