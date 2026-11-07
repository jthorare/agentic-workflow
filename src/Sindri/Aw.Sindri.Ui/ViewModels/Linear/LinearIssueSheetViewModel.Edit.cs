using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// The sheet's edit form (<see cref="LinearIssueSheetViewModel"/>, continued; Linear hub spec §3, "Issue sheet"): one form and one Save, only changed fields sent (D6),
/// the answers to a save, the conflict display and the question before unsaved edits go.
/// </summary>
internal sealed partial class LinearIssueSheetViewModel
{
    private const string NoWriteKey = "SND-LINEAR-002";
    private const string TeamNotPicked = "SND-LINEAR-003";

    private static readonly TimeSpan _checkAfter = TimeSpan.FromSeconds(15);

    private readonly UiContext _ui = new();
    private LinearIssueOne? _issue;
    private LinearLookups? _lookups;
    private Action? _then;
    private LinearChangeBody? _sent;
    private int _epoch;
    private bool _lookupsFailed;

    /// <summary>Focus asked for: the title after an applied save, the banner after a 409, Keep editing on the discard question.</summary>
    internal event EventHandler<ValueEventArgs<LinearIssueFocus>>? FocusRequested;

    /// <summary>The write-access banner's Connect asked for Settings → Connections → Linear.</summary>
    internal event EventHandler? ConnectRequested;

    /// <summary>Leaving with unsaved edits was confirmed (Discard) with no way on of its own: the shell closes the sheet.</summary>
    internal event EventHandler? LeaveConfirmed;

    /// <summary>True when the owner can edit the issue: the server said so, and the issue is still served.</summary>
    public bool CanEdit
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(ShowsNoDescription));
            OnChanged(nameof(CanSave));
            OnChanged(nameof(CanComment));
            OnChanged(nameof(CanPost));
        }
    }

    /// <summary>The banner over a form that cannot be edited ("Edits need write access."); empty when none.</summary>
    public string WriteBanner
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasWriteBanner));
        }
    } = string.Empty;

    /// <summary>True when the write-access banner shows.</summary>
    public bool HasWriteBanner => WriteBanner.Length > 0;

    /// <summary>True when the banner offers Connect (no write key).</summary>
    public bool CanConnect { get; private set => Set(ref field, value); }

    /// <summary>The Connect button's text.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string ConnectText => Labels.LinearConnectButton;
#pragma warning restore CA1822, MA0041

    /// <summary>True when the read-only sheet says the issue has no description (the edit form shows an empty box instead).</summary>
    public bool ShowsNoDescription => !HasDescription && !CanEdit;

    /// <summary>The title box.</summary>
    public LinearEditFieldViewModel TitleField => field ??= Watch(new LinearEditFieldViewModel("title", Labels.LinearIssueTitle, LinearEditKind.Text));

    /// <summary>The description box (plain multi-line text).</summary>
    public LinearEditFieldViewModel DescriptionField => field ??= Watch(new LinearEditFieldViewModel("description", Labels.LinearIssueDescription, LinearEditKind.Text));

    /// <summary>The form's grid, in the owner's order: state, priority, assignee, cycle, labels, project.</summary>
    public IReadOnlyList<LinearEditFieldViewModel> EditFields => field ??= [StateField, PriorityField, AssigneeField, CycleField, LabelsField, ProjectField];

    /// <summary>True when any field differs from what was opened.</summary>
    public bool HasChanges => Editable.Any(f => f.IsChanged);

    /// <summary>"2 changes: State, Priority"; empty with none.</summary>
    public string DirtyLine => Labels.LinearDirtyLine(ChangedNames());

    /// <summary>True when Save works: something changed and no save is out.</summary>
    public bool CanSave => CanEdit && HasChanges && !IsLocked;

    /// <summary>The Save button's text.</summary>
    public string SaveText => IsSaving ? Labels.LinearSending : Labels.LinearSave;

    /// <summary>True while a save is out.</summary>
    public bool IsSaving { get; private set => Set(ref field, value); }

    /// <summary>True while the fields are locked: a save is out, or its outcome is unknown.</summary>
    public bool IsLocked => IsSaving || IsUnknown;

    /// <summary>The polite line under the form: Saved, Checking, or Retry?; empty when none.</summary>
    public string StatusLine
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasStatusLine));
        }
    } = string.Empty;

    /// <summary>True when the status line shows.</summary>
    public bool HasStatusLine => StatusLine.Length > 0;

    /// <summary>The banner for a refused save; empty when none.</summary>
    public string ErrorBanner
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasErrorBanner));
        }
    } = string.Empty;

    /// <summary>True when the error banner shows.</summary>
    public bool HasErrorBanner => ErrorBanner.Length > 0;

    /// <summary>True after a save with no answer: Retry sends the same body.</summary>
    public bool CanRetry => IsUnknown;

    /// <summary>The Retry button's text.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string RetryText => Labels.LinearRetry;
#pragma warning restore CA1822, MA0041

    /// <summary>True while the 409 banner shows.</summary>
    public bool HasConflict { get; private set => Set(ref field, value); }

    /// <summary>"ENG-12 changed in Linear at 10:01."</summary>
    public string ConflictHeadline { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The line under the conflict headline.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string ConflictNote => Labels.LinearConflictNote;

    /// <summary>The banner's button that takes Linear's values.</summary>
    public string UseLinearsAllText => Labels.LinearUseLinearsAll;

    /// <summary>The banner's button that sends the owner's values again.</summary>
    public string SaveMineText => Labels.LinearSaveMine;

    /// <summary>The discard question's way back.</summary>
    public string KeepEditingText => Labels.LinearKeepEditing;

    /// <summary>The discard question's way out.</summary>
    public string DiscardText => Labels.LinearDiscard;
#pragma warning restore CA1822, MA0041

    /// <summary>The conflicting fields, Linear's value beside the owner's.</summary>
    public IReadOnlyList<LinearConflictRowViewModel> ConflictRows { get; private set => Set(ref field, value); } = [];

    /// <summary>True while the question "Discard changes to ENG-12?" shows.</summary>
    public bool IsConfirmingDiscard { get; private set => Set(ref field, value); }

    /// <summary>"Discard changes to ENG-12?"</summary>
    public string DiscardHeading => Labels.LinearDiscardHeading(Key);

    /// <summary>"Your edits to State and Priority aren't saved yet."</summary>
    public string DiscardBody => Labels.LinearDiscardBody(ChangedNames(), HasCommentText);

    private bool IsUnknown { get; set; }

    private LinearEditFieldViewModel StateField => field ??= Watch(new LinearEditFieldViewModel("stateId", Labels.LinearIssueField("state"), LinearEditKind.Choice));

    private LinearEditFieldViewModel PriorityField => field ??= Watch(new LinearEditFieldViewModel("priority", Labels.LinearIssueField("priority"), LinearEditKind.Choice));

    private LinearEditFieldViewModel AssigneeField => field ??= Watch(new LinearEditFieldViewModel("assigneeId", Labels.LinearIssueField("assignee"), LinearEditKind.Choice));

    private LinearEditFieldViewModel CycleField => field ??= Watch(new LinearEditFieldViewModel("cycleId", Labels.LinearIssueField("cycle"), LinearEditKind.Choice));

    private LinearEditFieldViewModel LabelsField => field ??= Watch(new LinearEditFieldViewModel("labelIds", Labels.LinearIssueField("labels"), LinearEditKind.Ticks));

    private LinearEditFieldViewModel ProjectField => field ??= new LinearEditFieldViewModel("projectId", Labels.LinearIssueField("project"), LinearEditKind.Fixed);

    // The body's order: title, then the grid in its order, then the description.
    private IEnumerable<LinearEditFieldViewModel> Editable => [TitleField, StateField, PriorityField, AssigneeField, CycleField, LabelsField, DescriptionField];

    /// <summary>Asks Settings → Connections → Linear to open (the write-access banner's Connect).</summary>
    internal void Connect() => ConnectRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Sends the changed fields (⌘S, the Save button). Nothing happens with no change or while a save is out.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer has been drawn.</returns>
    internal Task Save(CancellationToken cancellationToken) => CanSave ? Send(Body(), cancellationToken) : Task.CompletedTask;

    /// <summary>After a save with no answer: sends the identical body again.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer has been drawn.</returns>
    internal Task Retry(CancellationToken cancellationToken) => CanRetry && _sent is { } body ? Send(body, cancellationToken) : Task.CompletedTask;

    /// <summary>After a 409: sends the owner's values again; the baseline is the values Linear just showed.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer has been drawn.</returns>
    internal Task SaveMineAnyway(CancellationToken cancellationToken) => HasConflict && CanEdit && HasChanges && !IsLocked ? Send(Body(), cancellationToken) : Task.CompletedTask;

    /// <summary>After a 409: takes Linear's value for every listed field.</summary>
    internal void UseLinearsForAll()
    {
        foreach (var row in ConflictRows.ToList())
        {
            row.UseLinears();
        }
    }

    private LinearEditFieldViewModel Watch(LinearEditFieldViewModel field)
    {
        field.Edited += (_, _) => OnEdited();
        return field;
    }

    private void OnEdited()
    {
        if (!IsLocked)
        {
            (StatusLine, ErrorBanner) = (string.Empty, string.Empty);
        }

        OnChanged(nameof(HasChanges));
        OnChanged(nameof(DirtyLine));
        OnChanged(nameof(CanSave));
        OnChanged(nameof(DiscardBody));
        OnChanged(nameof(HasUnsaved));
        SettleQuestion();
    }

    private List<string> ChangedNames() => [.. Editable.Where(f => f.IsChanged).Select(f => f.Label)];

    private LinearChangeBody Body()
    {
        var (opened, set) = (new JsonObject(), new JsonObject());
        foreach (var field in Editable.Where(f => f.IsChanged))
        {
            opened[field.Api] = field.Baseline?.DeepClone();
            set[field.Api] = field.Current?.DeepClone();
        }

        return new LinearChangeBody(opened, set);
    }

    private void Lock(bool locked)
    {
        foreach (var field in Editable)
        {
            field.SetLocked(locked);
        }

        OnChanged(nameof(IsLocked));
        OnChanged(nameof(CanSave));
        OnChanged(nameof(CanRetry));
        OnChanged(nameof(SaveText));
    }
}
