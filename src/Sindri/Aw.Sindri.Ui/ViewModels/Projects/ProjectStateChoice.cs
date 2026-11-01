namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One state the owner can set on a project: the value the API takes and its word.</summary>
/// <param name="Value">"backlog", "planned", "inProgress", "paused", "done" or "canceled".</param>
/// <param name="Word">The word the sheet shows.</param>
internal sealed record ProjectStateChoice(string Value, string Word)
{
    /// <summary>The states the owner can pick, in the order the pickers list them.</summary>
    internal static IReadOnlyList<ProjectStateChoice> All { get; } =
    [
        new("backlog", "Backlog"), new("planned", "Planned"), new("inProgress", "In progress"), new("paused", "Paused"), new("done", "Done"), new("canceled", "Canceled"),
    ];
}
