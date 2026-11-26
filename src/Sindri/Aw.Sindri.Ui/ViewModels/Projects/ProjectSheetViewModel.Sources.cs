namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The sheet's typed sources (#4499, <see cref="ProjectSheetViewModel"/> continued): chips and the two edit-form fields.</summary>
internal sealed partial class ProjectSheetViewModel
{
    /// <summary>The edit form's typed branches, one per line (#4499).</summary>
    public string EditBranches { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The edit form's typed item ids, one per line (#4499).</summary>
    public string EditItems { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The typed branches and items as chips (#4499): one chip per branch (owner/name:branch, a trailing * for a prefix) and per item id.</summary>
    public IReadOnlyList<string> Sources { get; private set => Set(ref field, value); } = [];

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The branches field's label.</summary>
    public string BranchesLabel => Labels.ProjectBranchesField;

    /// <summary>The items field's label.</summary>
    public string ItemsLabel => Labels.ProjectItemsField;

    /// <summary>The heading above the chips.</summary>
    public string SourcesHeading => Labels.ProjectSourcesHeading;
}
