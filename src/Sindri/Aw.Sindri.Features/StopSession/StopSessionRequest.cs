using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.StopSession;

/// <summary>
/// End a session Sindri started (POST /sindri/sessions/{provider}/{id}/stop, 'sindri agents stop', MCP agents_stop; agents spec §15.2, issue 3147; human only): 'kill-session' on its 'sindri-&lt;actId&gt;' tmux session.
/// A session Sindri did not start is SND-AGENT-019; a session caller is SND-AUTH-002.
/// </summary>
/// <param name="Ref">The session: its provider and the tmux name 'sindri-&lt;actId&gt;' or the provider's session id.</param>
public sealed record StopSessionRequest(SessionRef Ref) : ICommand<StoppedSession>;
