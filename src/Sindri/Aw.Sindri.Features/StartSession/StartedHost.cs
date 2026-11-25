namespace Aw.Sindri.Features.StartSession;

/// <summary>Where a started session's pane is.</summary>
/// <param name="Socket">The tmux socket ('sindri-launch').</param>
/// <param name="Session">The tmux session name ('sindri-&lt;actId&gt;').</param>
/// <param name="Pane">The pane id ('%N').</param>
public sealed record StartedHost(string Socket, string Session, string Pane);
