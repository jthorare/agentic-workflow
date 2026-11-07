namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>Which review settings built the page: its name and where the repo matched ("review.projects[0]").</summary>
internal sealed record ReviewSettingsView(string Name, string From);
