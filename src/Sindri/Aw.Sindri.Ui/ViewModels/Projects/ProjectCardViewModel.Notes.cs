using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The card's notes area (projects page spec §3, §5.1): the whole text, an explicit edit with a counter, Save, Cancel and the 409's Reload.</summary>
internal sealed partial class ProjectCardViewModel
{
    private const string Conflict = "SND-PROJECT-409";

    private long _openedAt;   // the version the box was opened at: Save sends it, so a change made elsewhere meanwhile is a 409 and never a silent overwrite

    /// <summary>The notes as the API holds them, whole; empty when there are none.</summary>
    public string NotesText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when there is text to show.</summary>
    public bool HasNotes { get; private set => Set(ref field, value); }

    /// <summary>True while the box is open.</summary>
    public bool IsEditing { get; private set { Set(ref field, value); OnChanged(nameof(IsReading)); OnChanged(nameof(ShowsNotes)); Changed(); } }

    /// <summary>True while the notes are shown and not edited.</summary>
    public bool IsReading => !IsEditing;

    /// <summary>True when the notes text is drawn: there is some and the box is closed.</summary>
    public bool ShowsNotes => HasNotes && !IsEditing;

    /// <summary>What is typed in the box.</summary>
    public string Draft { get; set { Set(ref field, value); Changed(); } } = string.Empty;

    /// <summary>"1,204 / 16,384".</summary>
    public string Counter => Labels.ProjectNotesCounter(Draft.Length);

    /// <summary>True when the draft is longer than the API takes.</summary>
    public bool OverLimit => Draft.Length > Labels.ProjectNotesMax;

    /// <summary>True when Save would send: editing, not in flight, within the limit, and no conflict waiting for Reload.</summary>
    public bool CanSave => IsEditing && !IsSaving && !OverLimit && !IsConflict;

    /// <summary>True while the write is on its way.</summary>
    public bool IsSaving { get; private set { Set(ref field, value); OnChanged(nameof(SaveText)); Changed(); } }

    /// <summary>"Save", or "Saving…" while in flight.</summary>
    public string SaveText => IsSaving ? Labels.ProjectNotesSaving : Labels.ProjectNotesSave;

    /// <summary>Why the last Save did not go through (not a conflict); null when nothing is wrong.</summary>
    public string? SaveError { get; private set { Set(ref field, value); OnChanged(nameof(HasSaveError)); } }

    /// <summary>True when there is a line under Save.</summary>
    public bool HasSaveError => SaveError is not null;

    /// <summary>True after a 409 until Reload.</summary>
    public bool IsConflict { get; private set { Set(ref field, value); Changed(); } }

    /// <summary>After Reload, the notes the API holds now, shown above the box while it keeps what the owner typed; null otherwise.</summary>
    public string? NewerNotes { get; private set { Set(ref field, value); OnChanged(nameof(HasNewerNotes)); } }

    /// <summary>True when the newer notes show above the box.</summary>
    public bool HasNewerNotes => NewerNotes is not null;

    /// <summary>The polite live region for the notes ("Notes saved for …").</summary>
    public LiveAnnouncer Voice { get; } = new(Labels.ProjectNotesResting);

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The button that opens the box.</summary>
    public string EditText => Labels.ProjectNotesEdit;

    /// <summary>The button that drops the edit.</summary>
    public string CancelText => Labels.ProjectNotesCancel;

    /// <summary>The button that re-reads after a conflict.</summary>
    public string ReloadText => Labels.ProjectNotesReload;

    /// <summary>The caption over the newer notes after Reload.</summary>
    public string NewerNotesCaption => Labels.ProjectNotesNewer;

    /// <summary>The conflict line.</summary>
    public string ConflictText => Labels.ProjectNotesConflict;

    /// <summary>The box's accessible name.</summary>
    public string BoxName => Labels.ProjectNotesBoxName;
#pragma warning restore CA1822, MA0041

    /// <summary>Opens the box on the notes shown, at the version shown.</summary>
    internal void BeginEdit()
    {
        _openedAt = Card.Version;
        (SaveError, IsConflict, NewerNotes) = (null, false, null);
        Draft = NotesText;
        IsEditing = true;
    }

    /// <summary>Closes the box and drops what was typed.</summary>
    internal void CancelEdit()
    {
        (SaveError, IsConflict, NewerNotes) = (null, false, null);
        Draft = NotesText;
        IsEditing = false;
    }

    /// <summary>Sends the draft with the version the box was opened at, then re-reads the card so what is shown is what the API kept (it scrubs secrets).</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>A task that completes when the card shows the saved notes or the refusal.</returns>
    internal async Task Save(CancellationToken cancellationToken)
    {
        if (!CanSave || _api is null || _apiUrl?.Invoke() is not { } url)
        {
            return;
        }

        SaveError = null;
        IsSaving = true;
        try
        {
            var change = Draft.Length == 0 ? new ProjectChange(_openedAt, ClearNotes: true) : new ProjectChange(_openedAt, Notes: Draft);
            var written = await _api.ChangeProject(url, Id, change, cancellationToken).ConfigureAwait(true);
            if (written.IsError)
            {
                if (string.Equals(written.Error.Code, Conflict, StringComparison.Ordinal))
                {
                    IsConflict = true;   // the typed text stays until Reload
                }
                else
                {
                    SaveError = Labels.ProjectNotesRefused(written.Error);
                }

                return;
            }

            var one = await _api.Project(url, Id, cancellationToken).ConfigureAwait(true);
            if (one.IsSuccess)
            {
                Update(one.Value.Project);   // a failed read-back leaves the old text until the event stream redraws the card
            }

            (IsEditing, NewerNotes) = (false, null);
            Voice.Say(Labels.ProjectNotesSaved(Name));
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>Re-reads the card after a conflict: the box keeps what the owner typed, the notes the API holds now show above it, and the next Save goes at the newer version (#1710).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the box shows the card as it is now or the refusal.</returns>
    internal async Task Reload(CancellationToken cancellationToken)
    {
        if (!IsConflict || _api is null || _apiUrl?.Invoke() is not { } url)
        {
            return;
        }

        var one = await _api.Project(url, Id, cancellationToken).ConfigureAwait(true);
        if (one.IsError)
        {
            SaveError = Labels.ProjectNotesRefused(one.Error);
            return;
        }

        Update(one.Value.Project);
        _openedAt = Card.Version;
        NewerNotes = NotesText;
        (SaveError, IsConflict) = (null, false);
    }

    private void DeriveNotes()
    {
        NotesText = Card.Notes ?? string.Empty;
        HasNotes = !string.IsNullOrWhiteSpace(Card.Notes);
        OnChanged(nameof(ShowsNotes));
    }

    private void Changed()
    {
        OnChanged(nameof(Counter));
        OnChanged(nameof(OverLimit));
        OnChanged(nameof(CanSave));
    }
}
