namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Where a person came into a recipe's runs (GET /workflow/recipes/{id}/stats).</summary>
/// <param name="ParksAnswered">Parked runs a person answered.</param>
/// <param name="EscalationsDecided">Escalations a person decided.</param>
/// <param name="Takeovers">Runs a person took over.</param>
internal sealed record StatsHumanTimeView(HumanTimeFigureView ParksAnswered, HumanTimeFigureView EscalationsDecided, HumanTimeFigureView Takeovers);
