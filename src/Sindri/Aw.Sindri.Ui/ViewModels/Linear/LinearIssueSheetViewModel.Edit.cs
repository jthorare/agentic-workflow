#pragma warning disable CA1822, MA0041, MA0182 // why: the red commit's stubs have no state yet; the next commit gives them bodies
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>The sheet's edit form (skeleton: the red commit).</summary>
internal sealed partial class LinearIssueSheetViewModel
{
    /// <summary>Focus asked for.</summary>
    internal event EventHandler<ValueEventArgs<LinearIssueFocus>>? FocusRequested;

    /// <summary>Connect asked for.</summary>
    internal event EventHandler? ConnectRequested;

    /// <summary>Leaving was confirmed; the value is the issue to go to, or null.</summary>
    internal event EventHandler<ValueEventArgs<string?>>? LeaveConfirmed;

    public bool CanEdit => throw new NotSupportedException();

    public string WriteBanner => throw new NotSupportedException();

    public bool CanConnect => throw new NotSupportedException();

    public string ConnectText => throw new NotSupportedException();

    public LinearEditFieldViewModel TitleField => throw new NotSupportedException();

    public LinearEditFieldViewModel DescriptionField => throw new NotSupportedException();

    public IReadOnlyList<LinearEditFieldViewModel> EditFields => throw new NotSupportedException();

    public bool HasChanges => throw new NotSupportedException();

    public string DirtyLine => throw new NotSupportedException();

    public bool CanSave => throw new NotSupportedException();

    public string SaveText => throw new NotSupportedException();

    public bool IsSaving => throw new NotSupportedException();

    public bool IsLocked => throw new NotSupportedException();

    public string StatusLine => throw new NotSupportedException();

    public string ErrorBanner => throw new NotSupportedException();

    public bool CanRetry => throw new NotSupportedException();

    public bool HasConflict => throw new NotSupportedException();

    public string ConflictHeadline => throw new NotSupportedException();

    public string ConflictNote => throw new NotSupportedException();

    public IReadOnlyList<LinearConflictRowViewModel> ConflictRows => throw new NotSupportedException();

    public bool IsConfirmingDiscard => throw new NotSupportedException();

    public string DiscardHeading => throw new NotSupportedException();

    public string DiscardBody => throw new NotSupportedException();

    internal void Connect() => _ = (ConnectRequested, FocusRequested, LeaveConfirmed);

    internal Task Save(CancellationToken cancellationToken) => throw new NotSupportedException();

    internal Task Retry(CancellationToken cancellationToken) => throw new NotSupportedException();

    internal Task SaveMineAnyway(CancellationToken cancellationToken) => throw new NotSupportedException();

    internal void UseLinearsForAll() => throw new NotSupportedException();

    internal bool Escape() => throw new NotSupportedException();

    internal bool Leaving(string? next) => throw new NotSupportedException();

    internal void Discard() => throw new NotSupportedException();
}
