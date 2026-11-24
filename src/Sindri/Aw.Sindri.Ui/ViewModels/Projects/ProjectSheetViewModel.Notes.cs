namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The sheet's notes (<see cref="ProjectSheetViewModel"/>, continued; issue 3717): the rows have no notes area, so the sheet edits the notes through the page's
/// own row of the project (its <see cref="ProjectCardViewModel"/>, which holds the edit box, Save, Cancel and the 409's Reload). With no such row (a project
/// the page doesn't draw) the notes stay read-only.
/// </summary>
internal sealed partial class ProjectSheetViewModel
{
    /// <summary>The page's row of the project the sheet shows, whose notes area the sheet draws; null when the page has none.</summary>
    public ProjectCardViewModel? NotesCard
    {
        get;
        private set
        {
            Set(ref field, value);
            NotesPlaceChanged();
        }
    }

    /// <summary>True when the sheet draws the editable notes area.</summary>
    public bool HasNotesCard => NotesCard is not null;

    /// <summary>True when the sheet draws the notes as read-only text: there are notes and no row to edit them through.</summary>
    public bool ShowsReadOnlyNotes => NotesCard is null && !string.IsNullOrEmpty(Notes);

    /// <summary>True when the sheet draws its Notes heading.</summary>
    public bool HasNotesPlace => NotesCard is not null || !string.IsNullOrEmpty(Notes);

    /// <summary>The shell opened the sheet on a project: its row on the page, or null.</summary>
    /// <param name="card">The page's row of the project.</param>
    internal void EditNotesWith(ProjectCardViewModel? card) => NotesCard = card ?? _ownNotesCard;

    // A project the page draws no row for (another view, a hidden one, no read yet): the sheet makes a card from what it read, so its notes still have an edit box.
    private void KeepNotesCard(ProjectCardView served)
    {
        if (_ownNotesCard is { } own)
        {
            own.Update(served);
        }
        else if (NotesCard is null)
        {
            _ownNotesCard = new ProjectCardViewModel(served, _clock, Api, () => _url);
            NotesCard = _ownNotesCard;
        }
    }

    private void DropOwnNotesCard()
    {
        if (ReferenceEquals(NotesCard, _ownNotesCard))
        {
            NotesCard = null;
        }

        _ownNotesCard = null;
    }

    private void NotesPlaceChanged()
    {
        OnChanged(nameof(HasNotesCard));
        OnChanged(nameof(ShowsReadOnlyNotes));
        OnChanged(nameof(HasNotesPlace));
    }
}
