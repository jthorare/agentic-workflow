using Aw.Sindri.Core.Ids;
namespace Aw.Sindri.Features.StartSession;

/// <summary>What a start answers (agents spec §15.8): the row id, the caller's key, the state and where the tmux pane is.</summary>
/// <param name="Id">'sindri-&lt;actId&gt;' until the agent writes its fleet file; the row id is then 'session:&lt;provider&gt;/&lt;id&gt;'.</param>
/// <param name="ActId">The caller's key, echoed.</param>
/// <param name="State">'starting'.</param>
/// <param name="Host">The tmux socket, session and pane.</param>
public sealed record StartedSession(SessionId Id, ActId ActId, string State, StartedHost Host);
