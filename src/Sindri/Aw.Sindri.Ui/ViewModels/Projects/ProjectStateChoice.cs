namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One state the owner can set on a project: the value the API takes and its word.</summary>
/// <param name="Value">"backlog", "planned", "inProgress", "paused", "done" or "canceled".</param>
/// <param name="Word">The word the sheet shows.</param>
internal sealed record ProjectStateChoice(string Value, string Word);
