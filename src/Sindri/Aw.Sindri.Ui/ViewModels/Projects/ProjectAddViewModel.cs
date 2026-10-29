using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The Add project sheet (projects page spec §3.2): two tabs, From Linear (the default) and By hand. By hand takes a name, a state, links, notes and a target date; Save
/// sends them and, on success, raises <see cref="Added"/> so the shell closes the sheet and the page moves focus to the new card. A refusal shows under the
/// button and keeps every field.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="apiUrl">Where the API is now, or null while it is unreachable.</param>
internal sealed partial class ProjectAddViewModel(ISindriApi api, Func<Uri?> apiUrl) : ObservableObject
{
    private readonly ISindriApi _api = api;
    private readonly Func<Uri?> _apiUrl = apiUrl;

    /// <summary>A project was added: the shell closes the sheet, says it, and the page reads again and focuses the new card.</summary>
    internal event EventHandler<ProjectDoneEventArgs>? Added;

    /// <summary>Cancel was pressed: the shell closes the sheet.</summary>
    internal event EventHandler? Cancelled;

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The From Linear tab's word.</summary>
    public string FromLinearText => Labels.ProjectAddFromLinear;

    /// <summary>The By hand tab's word.</summary>
    public string ByHandText => Labels.ProjectAddByHand;

    /// <summary>The From Linear search box's watermark and accessible name.</summary>
    public string SearchLabel => Labels.ProjectAddSearch;

    /// <summary>The note under the From Linear list.</summary>
    public string LinearFootnote => Labels.ProjectAddLinearFootnote;

    /// <summary>The name field's label.</summary>
    public string NameLabel => Labels.ProjectNameField;

    /// <summary>The state picker's label.</summary>
    public string StateLabel => Labels.ProjectStateField;

    /// <summary>The links field's label.</summary>
    public string LinksLabel => Labels.ProjectLinksField;

    /// <summary>The notes field's label.</summary>
    public string NotesLabel => Labels.ProjectAddNotesField;

    /// <summary>The target date field's label.</summary>
    public string TargetLabel => Labels.ProjectTargetField;

    /// <summary>The Cancel button's word.</summary>
    public string CancelText => Labels.ProjectCancelEdit;
#pragma warning restore CA1822, MA0041

    /// <summary>True while the By hand tab shows.</summary>
    public bool IsByHand
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsFromLinear));
        }
    }

    /// <summary>True while the From Linear tab shows.</summary>
    public bool IsFromLinear => !IsByHand;

    /// <summary>The states the owner can pick.</summary>
    public IReadOnlyList<ProjectStateChoice> StateChoices { get; } = ProjectStateChoice.All;

    /// <summary>The state the picker holds; Planned until the owner picks.</summary>
    public ProjectStateChoice? SelectedState { get; set => Set(ref field, value); } = Planned;

    /// <summary>The name; Save waits for one.</summary>
    public string Name
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(CanSave));
        }
    } = string.Empty;

    /// <summary>The links, one per line.</summary>
    public string Links { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The owner's notes.</summary>
    public string Notes { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The target date, yyyy-MM-dd; empty for none.</summary>
    public string Target { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the write is in flight.</summary>
    public bool IsSaving
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(SaveText));
            OnChanged(nameof(CanSave));
        }
    }

    /// <summary>"Save", or "Saving…" while the write is in flight.</summary>
    public string SaveText => IsSaving ? Labels.ProjectSaving : Labels.ProjectSave;

    /// <summary>True when Save can be pressed: the name is not blank and no write is in flight.</summary>
    public bool CanSave => !IsSaving && Name.Trim().Length > 0;

    /// <summary>Under the button: why the last Save failed; empty otherwise.</summary>
    public string SaveError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Shows the From Linear tab and reads its list if this opening has not.</summary>
    internal void SelectFromLinear()
    {
        IsByHand = false;
        StartLinearLoad();
    }

    /// <summary>Shows the By hand tab.</summary>
    internal void SelectByHand() => IsByHand = true;

    /// <summary>Leaves the sheet without saving.</summary>
    internal void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>Starts the sheet over: From Linear, every field empty, no error, and the Linear list read again.</summary>
    internal void Reset()
    {
        (IsByHand, SelectedState, Name, Links, Notes, Target, SaveError) = (false, Planned, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        (_linearLoaded, _linearAll, _linearReading, _attaching) = (false, [], false, false);
        _linearGeneration++;   // why: a read still in flight belongs to the last opening
        (LinearError, LinearSearch) = (string.Empty, string.Empty);
        StartLinearLoad();
    }

    /// <summary>Adds the hand-made project the fields describe.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal async Task Save(CancellationToken cancellationToken)
    {
        if (!CanSave || _apiUrl() is not { } url)
        {
            return;
        }

        var target = Target.Trim();
        if (target.Length > 0 && !DateOnly.TryParseExact(target, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            SaveError = Labels.ProjectAddBadTarget;
            return;
        }

        var attach = Body(target);
        (SaveError, IsSaving) = (string.Empty, true);
        Result<ProjectAcceptedView> answer;
        try
        {
            answer = await _api.AttachProject(url, attach, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsSaving = false;
        }

        if (answer.IsError)
        {
            SaveError = Labels.ProjectAddFailed(answer.Error);
            return;
        }

        Added?.Invoke(this, new ProjectDoneEventArgs(ProjectDone.Added, answer.Value.Project, Labels.ProjectAdded(attach.Name!), wentToFinished: false));   // why: Body always sets the name
    }

    // The fields as the API takes them: an empty links, notes or target is left out.
    private ProjectAttach Body(string target)
    {
        var links = Links.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var notes = Notes.Trim();
        return new ProjectAttach(
            Name: Name.Trim(),
            State: SelectedState?.Value,
            Links: links.Length > 0 ? links : null,
            Notes: notes.Length > 0 ? notes : null,
            Target: target.Length > 0 ? target : null);
    }

    private static ProjectStateChoice Planned => ProjectStateChoice.All.First(s => string.Equals(s.Value, "planned", StringComparison.Ordinal));
}
