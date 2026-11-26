namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The sheet's typed sources (#4499, <see cref="ProjectSheetViewModel"/> continued): chips and the two edit-form fields.</summary>
internal sealed partial class ProjectSheetViewModel
{
    /// <summary>The edit form's typed branches, one per line (#4499); red stub: not filled or sent yet.</summary>
    public string EditBranches { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The edit form's typed item ids, one per line (#4499); red stub: not filled or sent yet.</summary>
    public string EditItems { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The typed branches and items as chips (#4499); red stub: always empty.</summary>
    public IReadOnlyList<string> Sources { get; private set => Set(ref field, value); } = [];
}
