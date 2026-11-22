using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.TerminalSocket;

/// <summary>Which tmux pane a session's terminal attaches to (agents spec §15.2). The terminal route asks it before the WebSocket upgrade, so a refusal answers as plain HTTP.</summary>
/// <param name="Session">The session: its provider and its provider session id, or the tmux session name 'sindri-&lt;actId&gt;'.</param>
public sealed record FindTerminalPaneRequest(SessionRef Session) : IQuery<FindTerminalPaneResponse>;
