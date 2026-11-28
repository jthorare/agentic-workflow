namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The states of a ranked list and of the tiles (<see cref="WholeMapViewModel"/>, continued; #3674): skeleton rows while a read runs, one error line with Retry, [ and ] for the pages, and where focus goes back to.</summary>
internal sealed partial class WholeMapViewModel
{
    private Action? _retry;
    private string? _lastChosen;

    /// <summary>Raised when focus belongs on a tile or row again (the key of the item): Esc came back to the list or tiles it was opened from.</summary>
    internal event EventHandler<ValueEventArgs<string>>? FocusReturned;

    /// <summary>Gets whether another page or tab of a list is being read: the rows show as a skeleton, never a spinner over the list.</summary>
    public bool ListLoading { get; private set; }

    /// <summary>Gets whether the last read of a list failed: one line with Retry.</summary>
    public bool IsListFailed { get; private set; }

    /// <summary>Gets the skeleton's rows.</summary>
    public IReadOnlyList<int> SkeletonRows { get; } = [.. Enumerable.Range(0, 8)];

    /// <summary>Gets the skeleton's accessible name.</summary>
    public string ListLoadingText { get; } = Labels.ContextWholeListLoading;

    /// <summary>Gets the failure line of a list.</summary>
    public string ListFailedText { get; } = Labels.ContextWholeListFailed;

    /// <summary>A list's next tab or page is being read.</summary>
    internal void ListReading()
    {
        (ListLoading, IsListFailed, _retry) = (true, false, null);
        Changed();
    }

    /// <summary>The read of a list failed: the old rows stay, with the line and Retry.</summary>
    /// <param name="retry">What Retry asks for again.</param>
    internal void ListReadFailed(Action retry)
    {
        ArgumentNullException.ThrowIfNull(retry);
        (ListLoading, IsListFailed, _retry) = (false, true, retry);
        Changed();
    }

    /// <summary>Retry was pressed.</summary>
    internal void RetryList()
    {
        if (_retry is { } retry)
        {
            retry();
        }
    }

    /// <summary>[ goes to the previous page of the list and ] to the next; nothing happens at the ends or when no list shows.</summary>
    /// <param name="next">True for ], false for [.</param>
    /// <returns>True when a list is on the stage, so the key is its own.</returns>
    internal bool PageKey(bool next)
    {
        if (!ShowAreaList)
        {
            return false;
        }

        if (next)
        {
            NextPage();
        }
        else
        {
            PreviousPage();
        }

        return true;
    }

    // The item a person came from, so Esc brings focus back to it.
    private void Chose(string key) => _lastChosen = key;

    private void ReturnFocus(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            FocusReturned?.Invoke(this, new ValueEventArgs<string>(key));
        }
    }
}
