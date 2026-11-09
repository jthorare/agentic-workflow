using System.Text.Json.Nodes;
using Aw.Kernel;

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
public sealed record RunAgentConfig(string Repo, IReadOnlyList<string> ReadOnlyRepos, string? Agent, string Prompt, IReadOnlyDictionary<string, string> Outputs, TimeSpan Timeout, IReadOnlyList<string> Connections, TimeSpan? Wall)
{
    /// <summary>The agents a node may name.</summary>
    public static IReadOnlyList<string> Agents { get; } = ["claude-code", "codex", "cursor"];

    /// <summary>How long a node runs when its config gives no 'timeout'.</summary>
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromHours(2);

    /// <summary>Reads a node's interpolated config.</summary>
    /// <param name="config">The config, with every reference replaced.</param>
    /// <returns>The config, or why it can't be used.</returns>
    public static Result<RunAgentConfig> Parse(JsonObject config) => throw new NotSupportedException("red");

    /// <summary>Problems in a node's config that the field types can't express: a bad agent, a bad output name or type, a reserved name, a bad duration.</summary>
    /// <param name="config">The node's config, as written.</param>
    /// <returns>One problem per field: the field's name and what to change.</returns>
    public static IReadOnlyList<(string Field, string Problem)> Problems(JsonObject config) => throw new NotSupportedException("red");
}
