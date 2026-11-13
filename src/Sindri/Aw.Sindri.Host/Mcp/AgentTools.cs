using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.RemoveSessionWorktree;
using Aw.Sindri.Features.StartSession;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The agents_* tools of the one-off start (agents spec §15.8, A7): agents_start and agents_worktree_remove. Each is one call to the service its HTTP route calls. Both are listed, and both are desk actions without
/// SessionOk, so every MCP caller (an agent session) is refused with SND-AUTH-002 in v1: the owner starts agents from the app or a terminal.
/// </summary>
internal static class AgentTools
{
    /// <summary>agents_start's name.</summary>
    internal const string Start = "agents_start";

    /// <summary>agents_worktree_remove's name.</summary>
    internal const string RemoveWorktree = "agents_worktree_remove";

    private static Tool StartTool { get; } = new()
    {
        Name = Start,
        Description = "Start a one-off agent in a tmux session on Sindri's own tmux server. For the person only: an agent session is refused (SND-AUTH-002). Answers the row id, the act id, the state 'starting' and the tmux socket, session and pane.",
        InputSchema = ContextTools.Schema(
            new JsonObject
            {
                ["repo"] = ContextTools.Property("string", "A repo of the profile"),
                ["agent"] = ContextTools.Property("string", "claude-code or codex"),
                ["prompt"] = ContextTools.Property("string", "The first message, at most 8,000 characters; leave out to start the agent at its own prompt"),
                ["worktree"] = ContextTools.Property("boolean", "True runs the agent in a new worktree of the repo"),
                ["actId"] = ContextTools.Property("string", "8 to 32 characters of a-z and 0-9; a replay of a started one answers the first result"),
            },
            required: ["repo", "agent", "actId"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = false },
    };

    private static Tool RemoveWorktreeTool { get; } = new()
    {
        Name = RemoveWorktree,
        Description = "Remove the worktree a one-off start made ('git worktree remove', never --force). For the person only: an agent session is refused (SND-AUTH-002). A tree with changes is SND-AGENT-018.",
        InputSchema = ContextTools.Schema(new JsonObject { ["actId"] = ContextTools.Property("string", "The act id the start used") }, required: ["actId"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = true, IdempotentHint = true, OpenWorldHint = false },
    };

    /// <summary>The two tools.</summary>
    internal static IReadOnlyList<Tool> All { get; } = [StartTool, RemoveWorktreeTool];

    /// <summary>Runs the tool named <paramref name="tool"/>.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> Call(string tool, IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) =>
        string.Equals(tool, Start, StringComparison.Ordinal) ? CallStart(services, arguments, ct) : CallRemove(services, arguments, ct);

    private static async ValueTask<CallToolResult> CallStart(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var request = StartRequest(arguments);
        if (request.IsError)
        {
            return McpResults.Failure(services, request.Error);
        }

        var answer = await services.GetRequiredService<IService<StartSessionRequest, StartedSession>>().Handle(request.Value, ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static a => $"started {a.Id.Value} (pane {a.Host.Pane})");
    }

    private static Result<StartSessionRequest> StartRequest(IDictionary<string, JsonElement>? arguments)
    {
        var repo = McpArguments.RequiredString(Start, arguments, "repo", "sindri");
        var agent = McpArguments.RequiredString(Start, arguments, "agent", "claude-code");
        var actId = McpArguments.RequiredString(Start, arguments, "actId", "k3j9x0a1");
        var prompt = McpArguments.OptionalString(Start, arguments, "prompt");
        var worktree = McpArguments.OptionalBool(Start, arguments, "worktree");
        if ((McpArguments.Only(Start, arguments, "repo", "agent", "prompt", "worktree", "actId") ?? Failed(repo) ?? Failed(agent) ?? Failed(actId) ?? Failed(prompt) ?? Failed(worktree)) is { } error)
        {
            return error;
        }

        if (!RepoName.TryFrom(repo.Value, out var repoName) || !AgentId.TryFrom(agent.Value, out var agentId))
        {
            return Core.SindriErrors.InvalidInput.With("repo is a repo name and agent an agent id, for example claude-code");
        }

        return ActId.TryFrom(actId.Value, out var act)
            ? new StartSessionRequest(repoName, agentId, prompt.Value, worktree.Value == true, act)
            : Core.SindriErrors.InvalidInput.With("actId is 8 to 32 characters of a-z and 0-9");
    }

    private static Error? Failed<T>(Result<T> result) => result.IsError ? result.Error : null;

    private static async ValueTask<CallToolResult> CallRemove(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var refused = McpArguments.Only(RemoveWorktree, arguments, "actId");
        var actId = McpArguments.RequiredString(RemoveWorktree, arguments, "actId", "k3j9x0a1");
        if ((refused ?? (actId.IsError ? actId.Error : null)) is { } error)
        {
            return McpResults.Failure(services, error);
        }

        var answer = await services.GetRequiredService<IService<RemoveSessionWorktreeRequest, RemovedWorktree>>().Handle(new RemoveSessionWorktreeRequest(ActId.From(actId.Value)), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static a => $"removed {a.Path}");
    }
}
