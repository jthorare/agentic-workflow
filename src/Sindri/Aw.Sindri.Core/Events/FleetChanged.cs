using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'fleet.changed' payload (collector spec CA-3): a session appeared, started asking, or went. Public, and so it holds no text:
/// no cwd, title, socket or pane.
/// </summary>
/// <param name="Provider">The agent.</param>
/// <param name="SessionId">The session's id.</param>
/// <param name="Change">What changed.</param>
/// <param name="State">The state when the change was seen.</param>
/// <param name="Reason">Why it is asking, for an 'asking' change.</param>
/// <param name="Repo">The profile repo, when resolved.</param>
/// <param name="Host">The host's kind ('tmux', 'warp', 'unknown').</param>
/// <param name="LaunchId">The Sindri launch the session belongs to, or null.</param>
public sealed record FleetChanged(
    AgentId Provider, SessionId SessionId, FleetChangeKind Change, SessionState State, AskingReason? Reason, RepoName? Repo, string Host, LaunchId? LaunchId);
