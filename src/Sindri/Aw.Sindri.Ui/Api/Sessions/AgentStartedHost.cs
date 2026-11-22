namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>Where a started session runs.</summary>
/// <param name="Socket">The tmux socket.</param>
/// <param name="Session">The tmux session's name.</param>
/// <param name="Pane">The pane id, %N.</param>
internal sealed record AgentStartedHost(string Socket, string Session, string Pane);
