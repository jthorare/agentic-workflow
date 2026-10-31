using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The sheet's write, Remove page and Esc (<see cref="PageSheetViewModel"/>, continued).</summary>
internal sealed partial class PageSheetViewModel
{
    // "index.pages[0].name: …", "index.pages[0].actions[1].name: …", "index.pages[0].actions[1].concepts[0]: …"; the part after the path is the sentence.
    [GeneratedRegex(@"^index\.pages\[\d+\](?:\.(?<field>name)|\.actions\[(?<action>\d+)\]\.(?:name|concepts\[\d+\]))?: (?<text>.+)$", RegexOptions.Singleline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RefusalPath { get; }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NonWord { get; }

    /// <summary>Raised when the Remove page question has just been drawn: focus goes to Cancel.</summary>
    internal event EventHandler? CancelRemoveFocusRequested;

    /// <summary>Raised when the Remove page question has closed: focus goes back to the Remove page button.</summary>
    internal event EventHandler? RemovePageFocusRequested;

    /// <summary>Raised when the discard question has just been drawn: focus goes to Keep editing.</summary>
    internal event EventHandler? KeepEditingFocusRequested;

    /// <summary>Raised when the discard question has closed: focus goes back to the Name field.</summary>
    internal event EventHandler? NameFocusRequested;

    /// <summary>Gets a value indicating whether a write is in flight.</summary>
    public bool IsSaving { get; private set; }

    /// <summary>Gets a value indicating whether the save button can be pressed.</summary>
    public bool CanSave => IsReady && !IsSaving;

    /// <summary>Gets the save button's words: "Saving…", "Save and approve" when only this page waits, else "Save".</summary>
    public string SaveText => IsSaving ? Labels.PageSheetSaving : _approve ? Labels.PageSheetSaveAndApprove : Labels.PageSheetSave;

    /// <summary>Gets the line under the buttons: the clash, a failure, or a refusal that names no field.</summary>
    public string Note { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the note shows.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>Gets a value indicating whether the Remove page button shows: Edit page only.</summary>
    public bool ShowRemovePage => IsOwnerPage && IsReady;

    /// <summary>Gets the Remove page button's words.</summary>
    public string RemovePageText { get; } = Labels.PageSheetRemovePage;

    /// <summary>Gets a value indicating whether the Remove page question shows.</summary>
    public bool IsConfirmingRemove { get; private set; }

    /// <summary>Gets the Remove page question.</summary>
    public string RemoveQuestion => Labels.PageSheetRemoveQuestion(_draft?.Name ?? Name.Trim());

    /// <summary>Gets the question's confirm button's words.</summary>
    public string RemoveConfirmText { get; } = Labels.PageSheetRemoveConfirm;

    /// <summary>Gets the question's cancel button's words.</summary>
    public string RemoveCancelText { get; } = Labels.PageSheetRemoveCancel;

    /// <summary>Gets a value indicating whether the discard question shows.</summary>
    public bool IsConfirmingDiscard { get; private set; }

    /// <summary>Gets the discard question.</summary>
    public string DiscardQuestion { get; } = Labels.PageSheetDiscardQuestion;

    /// <summary>Gets the discard button's words.</summary>
    public string DiscardText { get; } = Labels.PageSheetDiscard;

    /// <summary>Gets the keep editing button's words.</summary>
    public string KeepEditingText { get; } = Labels.PageSheetKeepEditing;

    /// <summary>Writes the page: trimmed names, empty action rows dropped, concepts in column order.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>When the answer is drawn.</returns>
    internal async Task Save(CancellationToken cancellationToken)
    {
        if (!CanSave || _draft is null)
        {
            return;
        }

        var kept = Actions.Where(static a => a.Name.Trim().Length > 0).ToList();
        var id = _draft.Id.Length > 0 ? _draft.Id : Slug(Name);
        var draft = _draft with
        {
            Id = id,
            Name = Name.Trim(),
            Actions = [.. kept.Select(static a => new PageActionDraft(a.Id, a.Name.Trim(), a.Action, a.TickedIds))],
        };
        await Write(new PageSave([draft], []), id, kept, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Asks before removing the page (Edit page only); focus goes to Cancel.</summary>
    internal void AskRemove()
    {
        if (!ShowRemovePage || IsSaving)
        {
            return;
        }

        IsConfirmingRemove = true;
        OnChanged(nameof(IsConfirmingRemove));
        OnChanged(nameof(RemoveQuestion));
        CancelRemoveFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Keeps the page: the question closes and focus goes back to Remove page.</summary>
    internal void CancelRemove()
    {
        if (!IsConfirmingRemove)
        {
            return;
        }

        IsConfirmingRemove = false;
        OnChanged(nameof(IsConfirmingRemove));
        RemovePageFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes the page, with the same approve rule as the save.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>When the answer is drawn.</returns>
    internal async Task Remove(CancellationToken cancellationToken)
    {
        if (!IsConfirmingRemove || _draft is null || IsSaving)
        {
            return;
        }

        IsConfirmingRemove = false;
        OnChanged(nameof(IsConfirmingRemove));
        await Write(new PageSave([], [_draft.Id]), _draft.Id, [], cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Esc, or the close affordance: a question that shows is answered with its safe choice; changes ask first; otherwise the sheet closes.</summary>
    /// <returns>True when the key was spent on the sheet.</returns>
    internal bool Escape()
    {
        if (IsSaving)
        {
            return true;
        }

        if (IsConfirmingRemove)
        {
            CancelRemove();
            return true;
        }

        if (IsConfirmingDiscard)
        {
            KeepEditing();
            return true;
        }

        if (HasChanges)
        {
            IsConfirmingDiscard = true;
            OnChanged(nameof(IsConfirmingDiscard));
            KeepEditingFocusRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        Close();
        return true;
    }

    /// <summary>Goes back to the fields from the discard question.</summary>
    internal void KeepEditing()
    {
        if (!IsConfirmingDiscard)
        {
            return;
        }

        IsConfirmingDiscard = false;
        OnChanged(nameof(IsConfirmingDiscard));
        NameFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Closes the sheet and drops the changes.</summary>
    internal void Close() => Closed?.Invoke(this, EventArgs.Empty);

    private async Task Write(PageSave save, string id, IReadOnlyList<PageSheetActionViewModel> rows, CancellationToken cancellationToken)
    {
        ShowNameError(string.Empty);
        foreach (var row in Actions)
        {
            row.Refuse(string.Empty);
        }

        (IsSaving, Note) = (true, string.Empty);
        ChangedSaving();
        PageSaved saved;
        try
        {
            saved = await _writes.Save(save, _hash, _approve, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsSaving = false;
            ChangedSaving();
        }

        switch (saved.State)
        {
            case PageSavedState.Approved or PageSavedState.Waiting:
                SavedId = id;
                Saved?.Invoke(this, EventArgs.Empty);
                Close();
                break;
            case PageSavedState.Clash:
                await ReadProfile(string.IsNullOrEmpty(id) ? [] : [id], cancellationToken).ConfigureAwait(true);   // the new hash, so Save again lands on top
                Say(Labels.PageSheetClash);
                OnChanged(nameof(SaveText));
                break;
            case PageSavedState.Refused:
                Refuse(saved.Message ?? string.Empty, rows);
                break;
            case PageSavedState.Failed:
            default:
                Say(Labels.PageSheetFailed);
                break;
        }
    }

    // A refusal's sentence goes under the field its path names; one that names none goes under the buttons.
    private void Refuse(string message, IReadOnlyList<PageSheetActionViewModel> rows)
    {
        var match = RefusalPath.Match(message);
        if (!match.Success)
        {
            Say(message);
        }
        else if (match.Groups["action"].Success)
        {
            var at = int.Parse(match.Groups["action"].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (at < rows.Count)
            {
                rows[at].Refuse(match.Groups["text"].Value);
            }
            else
            {
                Say(match.Groups["text"].Value);
            }
        }
        else if (match.Groups["field"].Success)
        {
            ShowNameError(match.Groups["text"].Value);
        }
        else
        {
            Say(match.Groups["text"].Value);
        }
    }

    private void ShowNameError(string text)
    {
        NameError = text;
        OnChanged(nameof(NameError));
        OnChanged(nameof(HasNameError));
    }

    private void Say(string note)
    {
        Note = note;
        OnChanged(nameof(Note));
        OnChanged(nameof(HasNote));
    }

    private void ChangedSaving()
    {
        OnChanged(nameof(IsSaving));
        OnChanged(nameof(CanSave));
        OnChanged(nameof(SaveText));
    }

    // The id the server would make from a name: words in lower case joined with hyphens.
    private static string Slug(string name) => string.Join('-', NonWord.Split(name.Trim().ToLowerInvariant()).Where(static w => w.Length > 0));
}
