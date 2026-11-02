using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>An action row's "Pick another" search (<see cref="PageSheetActionViewModel"/>, continued): what shows under the row; <see cref="PageSheetViewModel"/> reads and filters.</summary>
internal sealed partial class PageSheetActionViewModel
{
    private bool _quiet;

    /// <summary>Gets the Pick another button's words.</summary>
    public string PickAnotherText { get; } = Labels.PageSheetPickAnother;

    /// <summary>Gets the search box's name for a screen reader.</summary>
    public string PickBoxName { get; } = Labels.PageSheetActionPickBoxName;

    /// <summary>Gets the button that closes the search.</summary>
    public string PickBackText { get; } = Labels.PageSheetPickBack;

    /// <summary>Gets a value indicating whether the button shows: the page has a map surface to pick actions from.</summary>
    public bool CanPick
    {
        get;
        internal set => Set(ref field, value);
    }

    /// <summary>Gets a value indicating whether the search shows under the row.</summary>
    public bool IsPicking
    {
        get;
        internal set => Set(ref field, value);
    }

    /// <summary>Gets or sets the search text; the sheet filters once the typing rests.</summary>
    public string PickQuery
    {
        get;
        set
        {
            Set(ref field, value);
            if (!_quiet)
            {
                PickQueryChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    } = string.Empty;

    /// <summary>Gets the actions the search found.</summary>
    public ObservableCollection<PageMatch> PickRows { get; } = [];

    /// <summary>Gets the search's line: "Searching…", the empty answer or the failure; empty while rows show.</summary>
    public string PickStatus
    {
        get;
        internal set
        {
            Set(ref field, value);
            OnChanged(nameof(HasPickStatus));
        }
    } = string.Empty;

    /// <summary>Gets a value indicating whether the search has a line.</summary>
    public bool HasPickStatus => PickStatus.Length > 0;

    /// <summary>Raised when the search text is typed.</summary>
    internal event EventHandler? PickQueryChanged;

    /// <summary>Closes the search and empties it, without telling the sheet.</summary>
    internal void ClearPick()
    {
        _quiet = true;
        PickQuery = string.Empty;
        _quiet = false;
        PickStatus = string.Empty;
        PickRows.Clear();
        IsPicking = false;
    }
}
