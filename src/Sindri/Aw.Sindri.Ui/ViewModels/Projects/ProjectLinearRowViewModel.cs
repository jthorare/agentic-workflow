using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// One row of the Add project sheet's From Linear tab (projects page spec §3.2): the project's name, "Lead: X · state · n open issues" and an Attach button. The
/// name and lead came from Linear and are drawn as plain text. The sheet decides when Attach can be pressed (<see cref="CanAttach"/>) and handles the request.
/// </summary>
/// <param name="choice">The row as the API served it.</param>
internal sealed class ProjectLinearRowViewModel(ProjectLinearChoice choice) : ObservableObject
{
    /// <summary>Attach was pressed on this row.</summary>
    internal event EventHandler? AttachRequested;

    /// <summary>The key Attach sends: "linear:project/&lt;uuid&gt;".</summary>
    public string Key { get; } = choice.Key;

    /// <summary>Linear's name.</summary>
    public string Name { get; } = choice.Name;

    /// <summary>"Lead: Ana Ruiz · In progress · 6 open issues"; the lead and the state are left out when Linear has none.</summary>
    public string Detail { get; } = Labels.ProjectLinearDetail(choice.Lead, choice.State, choice.OpenIssues);

    /// <summary>The button's accessible name: "Attach Mobile push settings".</summary>
    public string AttachName { get; } = Labels.ProjectAddAttachName(choice.Name);

    /// <summary>True while this row's Attach is in flight.</summary>
    public bool IsAttaching
    {
        get;
        internal set
        {
            Set(ref field, value);
            OnChanged(nameof(AttachText));
        }
    }

    /// <summary>"Attach", or "Attaching…" while this row's write is in flight.</summary>
    public string AttachText => IsAttaching ? Labels.ProjectAddAttaching : Labels.ProjectAddAttach;

    /// <summary>False while any row's Attach is in flight.</summary>
    public bool CanAttach { get; internal set => Set(ref field, value); } = true;

    /// <summary>Asks the sheet to attach this project.</summary>
    internal void RequestAttach() => AttachRequested?.Invoke(this, EventArgs.Empty);
}
