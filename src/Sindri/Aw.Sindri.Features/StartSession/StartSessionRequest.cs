using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.StartSession;

/// <summary>
/// Start a one-off agent session in tmux (POST /sindri/sessions, 'sindri agents start', MCP 'agents_start'; agents spec §15.8; human only). The tmux session is named 'sindri-&lt;actId&gt;'.
/// Only a 2xx result is stored under <paramref name="ActId"/>; a replay of a started actId answers the first result and starts nothing.
/// </summary>
/// <param name="Repo">A repo of the profile.</param>
/// <param name="Agent">The catalog agent to run.</param>
/// <param name="Prompt">The first message; null starts the agent with none. At most 8,000 characters (SND-AGENT-017).</param>
/// <param name="Worktree">True runs the agent in a new worktree of the repo; false runs it in the repo folder.</param>
/// <param name="ActId">The caller's key, 8 to 32 characters of a-z and 0-9; anything else is a 400 with no SND-AGENT code.</param>
public sealed record StartSessionRequest(RepoName Repo, AgentId Agent, string? Prompt, bool Worktree, ActId ActId) : ICommand<StartedSession>;
