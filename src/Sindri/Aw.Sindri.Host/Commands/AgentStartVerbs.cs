using System.Security.Cryptography;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.RemoveSessionWorktree;
using Aw.Sindri.Features.StartSession;
using Aw.Sindri.Features.StopSession;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri agents start --repo R --agent A [--prompt T] [--worktree] [--act-id ID] [--json]' 'sindri agents worktree remove &lt;actId&gt; [--json]' and 'sindri agents stop &lt;provider&gt;/&lt;id&gt; [--json]' (agents spec §15.8, A7; issue 3147): clients of POST /sindri/sessions,
/// DELETE /sindri/sessions/{actId}/worktree and POST /sindri/sessions/{provider}/{id}/stop. They need the control plane and never start it. Exit 0 when started or removed, 1 when the control plane is down or the call was refused, 2 on a usage error.
/// Without --act-id the start makes one (12 characters of 0-9 and a-f). The plain output adds the command that attaches a terminal to the new session.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class AgentStartVerbs(ControlPlaneClient client, Output output)
{
    internal const string StartUsage = "agents start --repo <r> --agent claude-code|codex [--prompt <text>] [--worktree] [--act-id <id>] [--json]";
    internal const string StopUsage = "agents stop <provider>/<id> [--json]";
    internal const string RemoveUsage = "agents worktree remove <actId> [--json]";

    internal async Task<int> Start(string[] args, bool json, CancellationToken ct)
    {
        var parsed = ParseStart(args);
        if (parsed.IsError)
        {
            _ = await output.Error(parsed.Error).ConfigureAwait(false);
            return 2;
        }

        var answer = await client.Post<StartSessionRequest, StartedSession>("/sindri/sessions", parsed.Value, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Refused(answer.Error).ConfigureAwait(false);
        }

        var started = answer.Value.Value;
        return json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"started {started.Id.Value} (act id {started.ActId.Value}, pane {started.Host.Pane})\nattach: tmux -L {started.Host.Socket} attach -t ={started.Host.Session}\n", 0).ConfigureAwait(false);
    }

    internal async Task<int> RemoveWorktree(string[] args, bool json, CancellationToken ct)
    {
        if (args is not [{ Length: > 0 } actText] || !ActId.TryFrom(actText, out var actId))
        {
            _ = await output.Error(SindriErrors.InvalidInput.With("the one argument is the act id of the start (" + RemoveUsage + ")")).ConfigureAwait(false);
            return 2;
        }

        var answer = await client.Delete<RemovedWorktree>($"/sindri/sessions/{Uri.EscapeDataString(actId.Value)}/worktree", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Refused(answer.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"removed {answer.Value.Value.Path}\n", 0).ConfigureAwait(false);
    }

    internal async Task<int> Stop(string[] args, bool json, CancellationToken ct)
    {
        if (args is not [{ Length: > 0 } text] || text.Split('/') is not [var provider, var id] || !AgentId.TryFrom(provider, out var agent) || !SessionId.TryFrom(id, out var session))
        {
            _ = await output.Error(SindriErrors.InvalidInput.With("the one argument is the session, <provider>/<id> (" + StopUsage + ")")).ConfigureAwait(false);
            return 2;
        }

        var route = $"/sindri/sessions/{Uri.EscapeDataString(agent.Value)}/{Uri.EscapeDataString(session.Value)}/stop";
        var answer = await client.Post<StopSessionRequest, StoppedSession>(route, new StopSessionRequest(new SessionRef(agent, session)), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Refused(answer.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"stopped {answer.Value.Value.Id.Value}\n", 0).ConfigureAwait(false);
    }

    /// <summary>A refusal or a control plane that is down exits 1 (agents spec §15.8); only a usage error exits 2.</summary>
    private async Task<int> Refused(Error error)
    {
        _ = await output.Error(error).ConfigureAwait(false);
        return 1;
    }

    private static readonly string[] _valuedFlags = ["--repo", "--agent", "--prompt", "--act-id"];

    private static Result<StartSessionRequest> ParseStart(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var worktree = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--worktree", StringComparison.Ordinal))
            {
                worktree = true;
            }
            else if (_valuedFlags.Contains(args[i], StringComparer.Ordinal) && i + 1 < args.Length)
            {
                values[args[i]] = args[++i];
            }
            else
            {
                return SindriErrors.InvalidInput.With($"'{args[i]}' isn't an argument of this command (or it needs a value): {StartUsage}");
            }
        }

        return StartRequest(values, worktree);
    }

    private static Result<StartSessionRequest> StartRequest(Dictionary<string, string> values, bool worktree)
    {
        if (!values.TryGetValue("--repo", out var repo) || !values.TryGetValue("--agent", out var agent))
        {
            return SindriErrors.InvalidInput.With("--repo and --agent are required: " + StartUsage);
        }

        if (!RepoName.TryFrom(repo, out var repoName))
        {
            return SindriErrors.InvalidInput.With($"'{repo}' is not a repo name");
        }

        if (!AgentId.TryFrom(agent, out var agentId))
        {
            return SindriErrors.InvalidInput.With($"'{agent}' is not an agent id");
        }

        var actText = values.GetValueOrDefault("--act-id") ?? Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        return ActId.TryFrom(actText, out var act)
            ? new StartSessionRequest(repoName, agentId, values.GetValueOrDefault("--prompt"), worktree, act)
            : SindriErrors.InvalidInput.With($"'{actText}' is not an act id");
    }
}
