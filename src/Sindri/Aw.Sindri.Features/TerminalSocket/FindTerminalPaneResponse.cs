using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.TerminalSocket;

/// <summary>The tmux pane a session's terminal attaches to.</summary>
/// <param name="Pane">The pane: its socket, session name and pane id.</param>
public sealed record FindTerminalPaneResponse(TerminalTarget Pane);
