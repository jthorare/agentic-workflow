using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>"Pick another" (<see cref="PageSheetViewModel"/>, continued, pages spec §3.6): a search over the map's pages that repoints the page's map surface.</summary>
internal sealed partial class PageSheetViewModel
{
    /// <summary>How long the typing rests before the search reads.</summary>
    internal static readonly TimeSpan PickDebounce = TimeSpan.FromMilliseconds(250);

    /// <summary>The most rows the search shows.</summary>
    internal const int PickLimit = 50;

    private int _pickGeneration;

    /// <summary>Gets the Pick another button's words.</summary>
    public string PickAnotherText { get; } = Labels.PageSheetPickAnother;

    /// <summary>Gets the search box's name for a screen reader.</summary>
    public string PickBoxName { get; } = Labels.PageSheetPickBoxName;

    /// <summary>Gets the button that closes the search.</summary>
    public string PickBackText { get; } = Labels.PageSheetPickBack;

    /// <summary>Gets a value indicating whether the search shows.</summary>
    public bool IsPicking { get; private set; }

    /// <summary>Gets or sets the search text; the search reads once the typing rests.</summary>
    public string PickQuery
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            OnChanged(nameof(PickQuery));
            if (IsPicking)
            {
                Picking = ReadAfterRest(value.Trim());
            }
        }
    } = string.Empty;

    /// <summary>Gets the latest search: it ends once its answer is drawn.</summary>
    internal Task Picking { get; private set; } = Task.CompletedTask;

    /// <summary>Gets the pages the search found.</summary>
    public ObservableCollection<PageMatch> PickRows { get; } = [];

    /// <summary>Gets the search's line: "Searching…", the empty answer or the failure; empty while rows show.</summary>
    public string PickStatus { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the search has a line.</summary>
    public bool HasPickStatus => PickStatus.Length > 0;

    /// <summary>Raised when focus goes into the search box.</summary>
    internal event EventHandler? PickBoxFocusRequested;

    /// <summary>Raised when focus goes back to the Pick another button.</summary>
    internal event EventHandler? PickButtonFocusRequested;

    /// <summary>Opens the search and reads the first rows.</summary>
    internal void OpenPick()
    {
        if (!IsReady || IsPicking)
        {
            return;
        }

        EndActionPick();   // one search is open at a time
        IsPicking = true;
        OnChanged(nameof(IsPicking));
        Picking = ReadPick(PickQuery.Trim());
        PickBoxFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Closes the search without a pick.</summary>
    /// <returns>True when a search was open.</returns>
    internal bool ClosePick()
    {
        if (!IsPicking)
        {
            return false;
        }

        EndPick();
        PickButtonFocusRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Points the page at a found page's map surface and closes the search.</summary>
    /// <param name="match">The row of <see cref="PickRows"/>.</param>
    internal void Choose(PageMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (_draft is null)
        {
            return;
        }

        _draft = _draft with { Surface = match.Key };
        EndPick();
        OnChanged(nameof(FromMap));
        OnChanged(nameof(HasFromMap));
        OnChanged(nameof(HasChanges));
        PickButtonFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void EndPick()
    {
        _pickGeneration++;   // a read still running is dropped
        (IsPicking, PickQuery, PickStatus) = (false, string.Empty, string.Empty);
        PickRows.Clear();
        OnChanged(nameof(IsPicking));
        OnChanged(nameof(PickQuery));
        OnChanged(nameof(PickStatus));
        OnChanged(nameof(HasPickStatus));
    }

    // A search reads once the typing rests; a newer search drops this one.
    private async Task ReadAfterRest(string text)
    {
        var generation = ++_pickGeneration;
        await Task.Delay(PickDebounce, _clock, CancellationToken.None).ConfigureAwait(true);
        if (generation == _pickGeneration)
        {
            await ReadPick(text).ConfigureAwait(true);
        }
    }

    private async Task ReadPick(string text)
    {
        var generation = ++_pickGeneration;
        PickStatus = Labels.PageSheetPickSearching;
        OnChanged(nameof(PickStatus));
        OnChanged(nameof(HasPickStatus));
        var found = await _writes.Search(_draft?.Repo, text, PickLimit, CancellationToken.None).ConfigureAwait(true);
        if (generation != _pickGeneration)
        {
            return;
        }

        PickRows.Clear();
        if (found is null)
        {
            PickStatus = Labels.PageSheetPickFailed;
        }
        else
        {
            foreach (var match in found.Where(m => !string.Equals(m.Key, _draft?.Surface, StringComparison.Ordinal)))
            {
                PickRows.Add(match);
            }

            PickStatus = PickRows.Count == 0 ? NoneText(text) : string.Empty;
        }

        OnChanged(nameof(PickStatus));
        OnChanged(nameof(HasPickStatus));
    }

    // A blank search names no query, so its empty answer says the map has no other page instead.
    private static string NoneText(string text) => text.Trim().Length == 0 ? Labels.PageSheetPickNoPages : Labels.PageSheetPickNone(text);
}
