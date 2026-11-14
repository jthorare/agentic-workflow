namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>GET /sindri/sessions/last-start (agents spec §15.4): the repo and agent of the last 2xx start; both null when none.</summary>
/// <param name="Repo">The last repo's name.</param>
/// <param name="Agent">The last agent's wire id.</param>
internal sealed record AgentLastStartView(string? Repo, string? Agent);
