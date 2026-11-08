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
    public static Result<RunAgentConfig> Parse(JsonObject config) => throw new NotSupportedException();
}
