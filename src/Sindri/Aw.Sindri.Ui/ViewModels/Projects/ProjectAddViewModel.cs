using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The Add project sheet (projects page spec §3.2): two tabs, From Linear and By hand. By hand takes a name, a state, links, notes and a target date; Save
/// sends them and, on success, raises <see cref="Added"/> so the shell closes the sheet and the page moves focus to the new card. A refusal shows under the
/// button and keeps every field.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="apiUrl">Where the API is now, or null while it is unreachable.</param>
internal sealed class ProjectAddViewModel(ISindriApi api, Func<Uri?> apiUrl) : ObservableObject
{
#pragma warning disable CA1823, IDE0052, CS0067 // the red commit: Save and Added arrive with the implementation
    private readonly ISindriApi _api = api;
    private readonly Func<Uri?> _apiUrl = apiUrl;

    /// <summary>A project was added: the shell closes the sheet, says it, and the page reads again and focuses the new card.</summary>
    internal event EventHandler<ProjectDoneEventArgs>? Added;
#pragma warning restore CA1823, IDE0052, CS0067

    /// <summary>True while the By hand tab shows.</summary>
    public bool IsByHand
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsFromLinear));
        }
    } = true;

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

    /// <summary>Shows the From Linear tab.</summary>
    internal void SelectFromLinear() => IsByHand = false;

    /// <summary>Shows the By hand tab.</summary>
    internal void SelectByHand() => IsByHand = true;

    /// <summary>Starts the sheet over: By hand, every field empty, no error.</summary>
    internal void Reset() =>
        (IsByHand, SelectedState, Name, Links, Notes, Target, SaveError) = (true, Planned, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>Adds the hand-made project the fields describe.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal Task Save(CancellationToken cancellationToken) => throw new NotSupportedException();

    private static ProjectStateChoice Planned => ProjectStateChoice.All.First(s => string.Equals(s.Value, "planned", StringComparison.Ordinal));
}
