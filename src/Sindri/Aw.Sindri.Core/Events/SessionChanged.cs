using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'session.changed' payload (Agents page spec §7.2): a live session's state changed. Public, and so it holds no text: no cwd, title, socket, pane or question.
/// </summary>
/// <param name="Provider">The agent.</param>
/// <param name="SessionId">The session's id.</param>
/// <param name="State">The state it changed to.</param>
public sealed record SessionChanged(AgentId Provider, SessionId SessionId, SessionState State);
