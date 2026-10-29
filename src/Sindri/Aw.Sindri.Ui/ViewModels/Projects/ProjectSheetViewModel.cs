using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// A project card's sheet (projects page spec §3.2): every field of the card, its links, and the actions its origin allows. It reads the card on open and again
/// on every event while open; a write sends the version the sheet read, and a stale one is "Changed elsewhere since you opened it." with Reload.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="launcher">Opens a link in the system's browser.</param>
/// <param name="clock">The clock.</param>
internal sealed partial class ProjectSheetViewModel(ISindriApi api, ILauncher launcher, TimeProvider clock) : ObservableObject
{
    private readonly TimeProvider _clock = clock;
    private Uri? _url;
    private int _generation;
    private ProjectOne? _one;

    /// <summary>Asking to remove puts focus on Keep: the view moves it.</summary>
    internal event EventHandler? KeepFocusRequested;

    /// <summary>A write took the card off the page (or back).</summary>
    internal event EventHandler<ProjectDoneEventArgs>? Done;

    /// <summary>The project's id, unescaped; empty before the first.</summary>
    public string Id { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the read stands.</summary>
    public FramePhase Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsShown));
            OnChanged(nameof(IsLoading));
            OnChanged(nameof(IsProblem));
        }
    }

    /// <summary>True while a project is shown.</summary>
    public bool IsShown => Phase == FramePhase.Shown;

    /// <summary>True before the first answer.</summary>
    public bool IsLoading => Phase == FramePhase.Loading;

    /// <summary>True while an unreadable, unknown or refused answer is the whole sheet.</summary>
    public bool IsProblem => Phase is FramePhase.Unreadable or FramePhase.Refused or FramePhase.NotFound;

    /// <summary>The problem's sentence.</summary>
    public string ProblemText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The stale note: the last refresh failed and the fields are the last read's.</summary>
    public string Note { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The first read pending.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string LoadingText => Labels.ReadingProject;
#pragma warning restore CA1822, MA0041

    /// <summary>The project's name; empty until read.</summary>
    public string Heading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"attached", "by hand", or null.</summary>
    public string? OriginChip { get; private set => Set(ref field, value); }

    /// <summary>The state in words, "set by you" when the owner's.</summary>
    public string StateText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The health word, or null.</summary>
    public string? HealthWord { get; private set => Set(ref field, value); }

    /// <summary>The health dot's tone.</summary>
    public ChipTone HealthTone { get; private set => Set(ref field, value); } = ChipTone.Neutral;

    /// <summary>"Lead: you"; null without a lead.</summary>
    public string? LeadText { get; private set => Set(ref field, value); }

    /// <summary>"Target Jan 31"; null without one.</summary>
    public string? TargetText { get; private set => Set(ref field, value); }

    /// <summary>"12 open issues"; null when unknown.</summary>
    public string? IssuesText { get; private set => Set(ref field, value); }

    /// <summary>"3 open PRs (updated in the last 30 days)"; null when unknown.</summary>
    public string? PrsText { get; private set => Set(ref field, value); }

    /// <summary>"Active 2h ago", or null.</summary>
    public string? ActivityText { get; private set => Set(ref field, value); }

    /// <summary>"Done Oct 23", or null while open.</summary>
    public string? FinishedText { get; private set => Set(ref field, value); }

    /// <summary>Linear's description, or null.</summary>
    public string? Description { get; private set => Set(ref field, value); }

    /// <summary>The owner's notes, read-only here, or null.</summary>
    public string? Notes { get; private set => Set(ref field, value); }

    /// <summary>The card's links: Open in Linear, then the owner's.</summary>
    public IReadOnlyList<ProjectLinkItem> Links { get; private set => Set(ref field, value); } = [];

    /// <summary>One entry per repo: its pull request list, or plain text.</summary>
    public IReadOnlyList<ProjectLinkItem> Repos { get; private set => Set(ref field, value); } = [];

    /// <summary>The states the owner can pick.</summary>
    public IReadOnlyList<ProjectStateChoice> StateChoices { get; } =
    [
        new("backlog", "Backlog"), new("planned", "Planned"), new("inProgress", "In progress"), new("paused", "Paused"), new("done", "Done"), new("canceled", "Canceled"),
    ];

    /// <summary>The state the picker holds.</summary>
    public ProjectStateChoice? SelectedState { get; set => Set(ref field, value); }

    /// <summary>True when the card takes edits (attached or hand-made, not hidden).</summary>
    public bool CanEdit { get; private set => Set(ref field, value); }

    /// <summary>True when the name is editable (hand-made).</summary>
    public bool CanEditName { get; private set => Set(ref field, value); }

    /// <summary>True when the owner can set a state.</summary>
    public bool CanSetState { get; private set => Set(ref field, value); }

    /// <summary>True when the owner's state can be given back to Linear's.</summary>
    public bool CanClearState { get; private set => Set(ref field, value); }

    /// <summary>True when the attachment can be removed.</summary>
    public bool CanRemove { get; private set => Set(ref field, value); }

    /// <summary>True for a led project that is on the page.</summary>
    public bool CanHide { get; private set => Set(ref field, value); }

    /// <summary>True for a hidden project.</summary>
    public bool CanUnhide { get; private set => Set(ref field, value); }

    /// <summary>True while the edit form is open.</summary>
    public bool IsEditing { get; private set => Set(ref field, value); }

    /// <summary>The form's name.</summary>
    public string EditName { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The form's target date, yyyy-MM-dd; empty for none.</summary>
    public string EditTarget { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The form's links, one per line.</summary>
    public string EditLinks { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>True while a write is in flight.</summary>
    public bool IsSaving
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(SaveText));
        }
    }

    /// <summary>"Save", or "Saving…" while the write is in flight.</summary>
    public string SaveText => IsSaving ? Labels.ProjectSaving : Labels.ProjectSave;

    /// <summary>Under the button: why the last write failed; empty otherwise.</summary>
    public string SaveError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the last write found the card changed: the error line has Reload.</summary>
    public bool IsConflict { get; private set => Set(ref field, value); }

    /// <summary>True while the remove question is asked.</summary>
    public bool IsConfirmingRemove { get; private set => Set(ref field, value); }

    /// <summary>"Remove Checkout rewrite from your page?"</summary>
    public string RemoveQuestion { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The last finished write in words ("Checkout rewrite saved"); the shell says it.</summary>
    public string Announcement { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Names the project now, before any read.</summary>
    /// <param name="subject">The deep link's subject: the id, url-escaped as one segment.</param>
    internal void Show(string subject)
    {
        var id = Uri.UnescapeDataString(subject);
        if (string.Equals(id, Id, StringComparison.Ordinal))
        {
            return;
        }

        _generation++;
        Clear();
        Id = id;
        Phase = FramePhase.Loading;
    }

    /// <summary>Reads the project behind a subject and shows it.</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="subject">The deep link's subject.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Open(Uri url, string subject, CancellationToken cancellationToken)
    {
        Show(subject);
        _url = url;
        return Read(cancellationToken);
    }

    /// <summary>Reads the open project again (every event while the sheet is open); an edit or a write in progress keeps what it has.</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return Id.Length == 0 || IsEditing || IsSaving ? Task.CompletedTask : Read(cancellationToken);
    }

    /// <summary>Reads the card again after a conflict, dropping the form.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Reload(CancellationToken cancellationToken)
    {
        (IsEditing, IsConfirmingRemove, IsConflict, SaveError) = (false, false, false, string.Empty);
        return Read(cancellationToken);
    }

    /// <summary>Opens the edit form with the card's values.</summary>
    internal void BeginEdit()
    {
        if (_one is not { } one || !CanEdit)
        {
            return;
        }

        (EditName, EditTarget, EditLinks) = (one.Project.Name, one.Project.Target ?? string.Empty, string.Join('\n', OwnerLinks(one.Project)));
        (SaveError, IsConflict, IsEditing) = (string.Empty, false, true);
    }

    /// <summary>Closes the form without saving.</summary>
    internal void CancelEdit() => (IsEditing, SaveError, IsConflict) = (false, string.Empty, false);

    /// <summary>Asks "Remove … from your page?"; focus goes to Keep.</summary>
    internal void AskRemove()
    {
        if (!CanRemove)
        {
            return;
        }

        (RemoveQuestion, SaveError, IsConflict, IsConfirmingRemove) = (Labels.ProjectRemoveQuestion(Heading), string.Empty, false, true);
        KeepFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Keeps the project: the question closes.</summary>
    internal void Keep() => (IsConfirmingRemove, SaveError, IsConflict) = (false, string.Empty, false);

    private ISindriApi Api { get; } = api;

    private ILauncher Launcher { get; } = launcher;
}
