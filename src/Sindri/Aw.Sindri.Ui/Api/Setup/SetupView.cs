namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>
/// GET /sindri/setup (setup-once spec §7, API #458): the steps of setting Sindri up, in their fixed order (repo, profile, recipes, mcp, map). The answer's
/// asOf is not read: the status carries the freshness the app shows.
/// </summary>
/// <param name="Steps">The steps, in the order the API serves them.</param>
internal sealed record SetupView(IReadOnlyList<SetupStepView> Steps);
