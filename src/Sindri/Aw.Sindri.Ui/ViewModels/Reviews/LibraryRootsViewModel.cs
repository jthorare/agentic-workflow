using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>
/// Settings' Library card (review page spec §6.3, §16): the folders the design library reads, per kind. The aw folder has Turn off, a vault has Remove, a repo's folder is read-only
/// ("set by the repo list"), and Add a folder opens the form. An agent session is served no folders, so it gets the empty words and nothing to edit. Every write approves in one
/// click when it is the only waiting profile change; otherwise the card says the change waits.
/// </summary>
internal sealed class LibraryRootsViewModel : ObservableObject
{
    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private bool _loaded;
    private int _generation;

    /// <summary>The card over the API and the folder chooser.</summary>
    /// <param name="url">The API's base URL, or null while it isn't known.</param>
    /// <param name="api">The API.</param>
    /// <param name="folders">The folder chooser (the form's Browse).</param>
    /// <param name="clock">The clock the scan ages are counted from.</param>
    internal LibraryRootsViewModel(Func<Uri?> url, ISindriApi api, IFolderPicker folders, TimeProvider clock)
    {
        (_url, _api, _clock) = (url, api, clock);
        Form = new LibraryRootFormViewModel(url, api, folders, Applied);
    }

#pragma warning disable CA1822, MA0041 // why: the view binds these as instance properties of the view model
    /// <summary>The card's heading.</summary>
    public string Title => Labels.LibraryFoldersHeading;

    /// <summary>The line under the heading.</summary>
    public string IntroText => Labels.LibraryFoldersIntro;

    /// <summary>The words before the first answer.</summary>
    public string ReadingText => Labels.LibraryFoldersReading;

    /// <summary>The words when there are no folders.</summary>
    public string EmptyText => CanAdd ? Labels.LibraryFoldersEmpty : Labels.LibraryFoldersNone;

    /// <summary>The button that opens the form.</summary>
    public string AddText => Labels.LibraryFoldersAdd;

    /// <summary>The Remove confirmation's confirm button.</summary>
    public string ConfirmRemoveText => Labels.LibraryFoldersRemove;

    /// <summary>The Remove confirmation's cancel button.</summary>
    public string CancelRemoveText => Labels.Cancel;
#pragma warning restore CA1822, MA0041

    /// <summary>The Add a folder form.</summary>
    public LibraryRootFormViewModel Form { get; }

    /// <summary>The folders, in served order: aw, vaults, then repo folders.</summary>
    public IReadOnlyList<LibraryFolderViewModel> Rows { get; private set => Set(ref field, value); } = [];

    /// <summary>True until the first answer.</summary>
    public bool IsLoading => !_loaded && ReadError.Length == 0;

    /// <summary>True when the first answer holds no folders (an agent session's).</summary>
    public bool IsEmpty => _loaded && Rows.Count == 0;

    /// <summary>True when there are folders to list.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>True when the owner can add a folder: the card has folders, so it is not a session's.</summary>
    public bool CanAdd => HasRows && !Form.IsOpen;

    /// <summary>True while a remove or a turn-off is on its way, so a second press does nothing.</summary>
    public bool IsBusy
    {
        get;
        private set
        {
            Set(ref field, value);
            foreach (var row in Rows)
            {
                row.SetBusy(value);
            }
        }
    }

    /// <summary>The line after a change that waits for the profile approval; empty otherwise.</summary>
    public string Note
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasNote));
        }
    } = string.Empty;

    /// <summary>True when a change waits for the approval.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>The failure of the first read; empty otherwise. A later read clears only this one.</summary>
    public string ReadError
    {
        get;
        private set
        {
            Set(ref field, value);
            ErrorChanged();
        }
    } = string.Empty;

    /// <summary>The refusal of the last remove or turn-off; stays until the next change.</summary>
    public string ChangeError
    {
        get;
        private set
        {
            Set(ref field, value);
            ErrorChanged();
        }
    } = string.Empty;

    /// <summary>The error to show: a change's refusal first, else the read's failure.</summary>
    public string Error => ChangeError.Length > 0 ? ChangeError : ReadError;

    /// <summary>True when there is an error to show.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Reads the folders. A failure is said only while there is nothing to show yet; a later one keeps the folders.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the card is up to date.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        var generation = ++_generation;
        var read = await _api.Library(url, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;   // a newer read started meanwhile; this older answer must not bring back what the newer one dropped
        }

        if (read.IsError)
        {
            if (!_loaded)
            {
                ReadError = Labels.LibraryFoldersReadFailed(read.Error);
            }

            return;
        }

        var now = _clock.GetUtcNow();
        var kept = Rows.ToDictionary(r => r.Name, StringComparer.Ordinal);
        Rows = [.. read.Value.Roots.Select(r => Kept(kept, r, now))];
        _loaded = true;
        ReadError = string.Empty;
        OnChanged(nameof(IsLoading));
        OnChanged(nameof(IsEmpty));
        OnChanged(nameof(HasRows));
        OnChanged(nameof(CanAdd));
        OnChanged(nameof(EmptyText));
    }

    // A folder already shown keeps its row (an open Remove question and the busy state stay); a new one gets a row.
    private LibraryFolderViewModel Kept(Dictionary<string, LibraryFolderViewModel> kept, LibraryFolderView folder, DateTimeOffset now)
    {
        if (kept.TryGetValue(folder.Name, out var row))
        {
            row.Update(folder, now);
            return row;
        }

        var added = new LibraryFolderViewModel(folder, now);
        added.SetBusy(IsBusy);
        return added;
    }

    private void ErrorChanged()
    {
        OnChanged(nameof(Error));
        OnChanged(nameof(HasError));
        OnChanged(nameof(IsLoading));
    }

    /// <summary>Removes a vault (the row has asked and been confirmed), approving the change.</summary>
    /// <param name="row">The vault's row.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Remove(LibraryFolderViewModel row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (IsBusy)
        {
            return Task.CompletedTask;   // the buttons are disabled meanwhile; a call that still gets here leaves the question up rather than dropping the remove
        }

        row.CancelRemove();
        return Change(url => _api.RemoveLibraryRoot(url, row.Name, approve: true, cancellationToken), cancellationToken);
    }

    /// <summary>Turns the aw folder on or off, approving the change.</summary>
    /// <param name="on">True to read it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task TurnAw(bool on, CancellationToken cancellationToken) =>
        Change(url => _api.SetLibraryAw(url, new LibraryAwSwitch(on, Approve: true), cancellationToken), cancellationToken);

    /// <summary>A row's errors chip was pressed: the shell opens the library errors sheet.</summary>
    internal event EventHandler? ErrorsRequested;

    /// <summary>Asks for the library errors sheet.</summary>
    internal void OpenErrors() => ErrorsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Opens the Add a folder form.</summary>
    internal void OpenForm()
    {
        Form.Open();
        OnChanged(nameof(CanAdd));
    }

    /// <summary>Closes the Add a folder form.</summary>
    internal void CloseForm()
    {
        Form.Cancel();
        OnChanged(nameof(CanAdd));
    }

    private async Task Change(Func<Uri, Task<Result<LibraryRootChanged>>> send, CancellationToken cancellationToken)
    {
        if (IsBusy || _url() is not { } url)
        {
            return;
        }

        IsBusy = true;
        Result<LibraryRootChanged> changed;
        try
        {
            changed = await send(url).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }

        if (changed.IsError)
        {
            ChangeError = Labels.LibraryFoldersFailed(changed.Error);
            await Refresh(url, cancellationToken).ConfigureAwait(true);   // a 404 on a vault removed elsewhere drops its row
            return;
        }

        ChangeError = string.Empty;
        await Applied(changed.Value, cancellationToken).ConfigureAwait(true);
    }

    // A change that went through: said when it waits for the approval, and the folders are read again.
    private async Task Applied(LibraryRootChanged changed, CancellationToken cancellationToken)
    {
        Note = changed.Approved ? string.Empty : Labels.ReviewsLoginPending;
        OnChanged(nameof(CanAdd));
        if (_url() is { } url)
        {
            await Refresh(url, cancellationToken).ConfigureAwait(true);
        }
    }
}
