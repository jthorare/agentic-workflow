namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>What the dialog says under its fields and through its polite line (<see cref="NewLinearIssueViewModel"/>, continued).</summary>
internal sealed partial class NewLinearIssueViewModel
{
    /// <summary>"Title is required" after a blank submit, until a title is typed; else empty.</summary>
    public string TitleError
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasTitleError));
            OnChanged(nameof(TitleErrorLine));
        }
    } = string.Empty;

    /// <summary>True while <see cref="TitleError"/> shows.</summary>
    public bool HasTitleError => TitleError.Length > 0;

    /// <summary>The error line under the title: the error, or a no-break space that keeps the line's height.</summary>
    public string TitleErrorLine => HasTitleError ? TitleError : " ";

    /// <summary>"Team is required" after a submit with no team, until one is picked; else empty.</summary>
    public string TeamError
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasTeamError));
            OnChanged(nameof(TeamErrorLine));
        }
    } = string.Empty;

    /// <summary>True while <see cref="TeamError"/> shows.</summary>
    public bool HasTeamError => TeamError.Length > 0;

    /// <summary>The error line under the team: the error, or a no-break space that keeps the line's height.</summary>
    public string TeamErrorLine => HasTeamError ? TeamError : " ";

    /// <summary>The live region's gate, set before its text changes.</summary>
    public bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>The live region's text; never empty.</summary>
    public string Announcement { get; private set => Set(ref field, value); } = Labels.LinearNewTitleRequired;

    private void RequireTitle(string text)
    {
        TitleError = text;
        Say(text);
        TitleFocusRequests++;
    }

    private void RequireTeam()
    {
        TeamError = Labels.LinearNewTeamRequired;
        Say(Labels.LinearNewTeamRequired);
        TeamFocusRequests++;
    }

    private void ClearTitleError()
    {
        if (HasTitleError)
        {
            TitleError = string.Empty;
        }

        IsAnnouncing = false;   // the region goes dark first; its text stays (never empty) and is not spoken
    }

    private void ClearTeamError()
    {
        if (HasTeamError)
        {
            TeamError = string.Empty;
            IsAnnouncing = false;
        }
    }

    // Says a text through the polite region: dark first, the resting text set, then live, then the text (the flag changes
    // before the text), so the resting text is never spoken and a text is spoken once.
    private void Say(string text)
    {
        if (IsAnnouncing && string.Equals(Announcement, text, StringComparison.Ordinal))
        {
            IsAnnouncing = false;
        }

        if (!IsAnnouncing)
        {
            Announcement = Labels.LinearNewTitleRequired;
            IsAnnouncing = true;
        }

        Announcement = text;
    }
}
