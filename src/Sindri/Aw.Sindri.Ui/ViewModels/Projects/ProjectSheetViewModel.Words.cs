namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The sheet's fixed words (<see cref="ProjectSheetViewModel"/>, continued): properties because the view binds them.</summary>
internal sealed partial class ProjectSheetViewModel
{
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The Edit button.</summary>
    public string EditText => Labels.ProjectEdit;

    /// <summary>The Cancel button of the form.</summary>
    public string CancelEditText => Labels.ProjectCancelEdit;

    /// <summary>The Set state button.</summary>
    public string SetStateText => Labels.ProjectSetState;

    /// <summary>The button that gives the state back to Linear's.</summary>
    public string UseLinearStateText => Labels.ProjectUseLinearState;

    /// <summary>The Remove button, and the question's confirm.</summary>
    public string RemoveText => Labels.ProjectRemove;

    /// <summary>The question's Keep.</summary>
    public string KeepText => Labels.ProjectKeep;

    /// <summary>The Hide button.</summary>
    public string HideText => Labels.ProjectHide;

    /// <summary>The Unhide button.</summary>
    public string UnhideText => Labels.ProjectUnhide;

    /// <summary>The conflict's Reload.</summary>
    public string ReloadText => Labels.ProjectReload;

    /// <summary>The name field's label.</summary>
    public string NameLabel => Labels.ProjectNameField;

    /// <summary>The target field's label.</summary>
    public string TargetLabel => Labels.ProjectTargetField;

    /// <summary>The links field's label.</summary>
    public string LinksLabel => Labels.ProjectLinksField;

    /// <summary>The state picker's label.</summary>
    public string StateLabel => Labels.ProjectStateField;

    /// <summary>The notes heading.</summary>
    public string NotesHeading => Labels.ProjectNotesHeading;

    /// <summary>The links heading.</summary>
    public string LinksHeading => Labels.ProjectLinksHeading;

    /// <summary>The repos heading.</summary>
    public string ReposHeading => Labels.ProjectReposHeading;
#pragma warning restore CA1822, MA0041
}
