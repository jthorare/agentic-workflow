namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>The 2xx answer of POST /sindri/sessions (agents spec §15.8): the new session in its Starting state.</summary>
/// <param name="Id">The row id: sindri-&lt;actId&gt; until the agent writes its fleet file.</param>
/// <param name="ActId">The key the form sent.</param>
/// <param name="State">Always starting.</param>
/// <param name="Host">Where the tmux session runs.</param>
internal sealed record AgentStartedView(string Id, string ActId, string State, AgentStartedHost Host);
